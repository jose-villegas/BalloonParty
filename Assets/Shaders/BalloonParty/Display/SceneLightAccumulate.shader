Shader "Hidden/BalloonParty/SceneLightAccumulate"
{
    // The first step of the light-field chain, in two passes over the same list of lights.
    //
    // Pass 0, the field: starts from a hardcoded rest state (R=0, GB=0.5, A=0) and ADDS every registered
    // light's magnitude into R, tagging A with the palette index of the tagged light that contributes most
    // at each texel. GB outputs the neutral direction constant (0.5) — the gradient pass recomputes it from
    // R afterwards. The rest state is written here, so no separate fill blit runs first.
    //
    // Pass 1, the colour map (a smaller target): what colour the local light is at each texel. Every
    // tagged light adds its palette colour by how much light it adds there, over all the light there, so
    // RGB is the tagged lights' blended colour times A, and A the share of the local light that is tagged.
    // A consumer reads the light's colour as key × (1 - A) + RGB: one filtered tap, a smooth blend between
    // two coloured lights, and a colour that fades with the light's own falloff. The index in the field's A
    // can't do that: an index can't be filtered, so its regions end on texel edges.
    //
    // Mirrors Grid/DisturbanceStampBatched: up to 32 lights per blit, aspect-corrected radial falloff,
    // (index+1)/16 palette encoding. With _StampCount = 0 both loops are skipped: the field is the pure
    // rest state and the colour map is empty.
    Properties
    {
        _MainTex     ("Field (read)",   2D)    = "black" {}
        _MaxBoost    ("Max Boost (R)",  Float) = 3.0
        _StampCount  ("Stamp Count",    Int)   = 0
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        #define MAX_STAMPS 32

        // A tagged light claims a texel's index only where it is at least this much of its own peak: its
        // faint outer ring is left untagged, for the receivers that drop a colour's light by that index.
        #define TAG_MIN_FALLOFF 0.2

        sampler2D _MainTex; // required by Graphics.Blit binding; not sampled (rest state hardcoded)
        float     _MaxBoost;    // ceiling on the summed additive boost above rest (soft-clamped)
        float     _StampAspect; // field height/width; corrects UV anisotropy so stamps stay circular
        int       _StampCount;

        float4 _StampCenters[MAX_STAMPS];      // xy = UV center
        float  _StampRadii[MAX_STAMPS];        // UV radius at the segment start
        float  _StampEndRadii[MAX_STAMPS];     // UV radius at the segment end; == start for uniform width
        float  _StampMagnitudes[MAX_STAMPS];   // peak magnitude at the centre (>= 0)
        float  _StampFalloffs[MAX_STAMPS];     // per-light radial falloff exponent (1 = linear cone)
        float  _StampColorIndices[MAX_STAMPS]; // encoded palette index; 0 = no color

        // The game palette, a global pushed by SceneLightFieldService in the slot order the indices name.
        float4 _SceneLightPalette[16];

        struct appdata
        {
            float4 vertex   : POSITION;
            float2 texcoord : TEXCOORD0;
        };

        struct v2f
        {
            float4 vertex : SV_POSITION;
            float2 uv     : TEXCOORD0;
        };

        v2f vert(appdata v)
        {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv     = v.texcoord;
            return o;
        }

        // How much of its own peak light s gives at uv: 1 on its axis, 0 at its radius.
        float StampFalloff(int s, float2 uv)
        {
            float2 a = _StampCenters[s].xy;   // segment start (UV)
            float2 b = _StampCenters[s].zw;   // segment end (UV); == a for a point light

            // Distance from the pixel to the segment [a, b], aspect-corrected so the metric is
            // circular in world space. A point light (a == b) reduces to distance-to-point; a
            // segment gives a capsule — full along the axis, decaying to the sides = an area beam.
            float2 pa = uv - a;
            float2 ba = b - a;
            pa.y *= _StampAspect;
            ba.y *= _StampAspect;
            float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-8));
            float dist = length(pa - ba * h);

            // The half-width tapers along the axis (h = 0 at the start, 1 at the end) — a cone
            // beam. Equal radii lerp to the same value, so uniform capsules are untouched. The
            // gradient pass derives GB from R, so the taper shapes the direction field too.
            float radius = lerp(_StampRadii[s], _StampEndRadii[s], h);

            // Smooth radial falloff, peak on the axis → 0 at the radius, shaped by the light's own
            // falloff exponent (1 = linear, higher = more concentrated). No plateau, so R (and the
            // direction the gradient pass derives from it) varies continuously.
            float t = saturate(1.0 - dist / max(radius, 1e-4));
            return pow(t, _StampFalloffs[s]);
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Rest state hardcoded, so no fill blit runs before this one. R=0: no local boost.
                // GB=0.5: neutral direction. A=0: no palette tag.
                float rRest = 0.0;
                float bestIndex = 0.0;

                float summedBoost = 0.0;
                float bestContribution = 0.0;

                for (int s = 0; s < _StampCount; s++)
                {
                    float falloff = StampFalloff(s, uv);
                    float contribution = falloff * _StampMagnitudes[s];
                    summedBoost += contribution;

                    float encoded = _StampColorIndices[s];
                    if (encoded > 0.001 && contribution > bestContribution && falloff > TAG_MIN_FALLOFF)
                    {
                        bestContribution = contribution;
                        bestIndex = encoded;
                    }
                }

                // Soft clamp: the summed boost approaches _MaxBoost asymptotically, so overlapping
                // lights never blow R out yet stay linear (~= summedBoost) while small. At
                // summedBoost = 0 this is exactly 0, leaving rRest untouched (the count-0 identity).
                float boost = _MaxBoost * (1.0 - exp(-summedBoost / _MaxBoost));

                return float4(rRest + boost, 0.5, 0.5, bestIndex);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                float all = 0.0;
                float tagged = 0.0;
                float3 colour = float3(0.0, 0.0, 0.0);

                for (int s = 0; s < _StampCount; s++)
                {
                    float contribution = StampFalloff(s, uv) * _StampMagnitudes[s];
                    all += contribution;

                    float encoded = _StampColorIndices[s];
                    if (encoded > 0.001)
                    {
                        int index = (int)min(floor(encoded * 16.0 + 0.5) - 1.0, 15.0);
                        colour += _SceneLightPalette[index].rgb * contribution;
                        tagged += contribution;
                    }
                }

                // Shares of the light here, so a light's colour holds to its very edge and only its
                // brightness fades: a consumer scales the colour by the field's R itself. No light, no share.
                float share = 1.0 / max(all, 1e-5);
                return float4(colour * share, tagged * share);
            }
            ENDCG
        }
    }
}
