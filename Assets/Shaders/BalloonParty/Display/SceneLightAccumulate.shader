Shader "Hidden/BalloonParty/SceneLightAccumulate"
{
    // Pass 1 of the light-field chain (Fill merged in): starts from a hardcoded rest state
    // (R=0, GB=0.5, A=0) and ADDS every registered light's magnitude into R, tagging A with the
    // palette index of the light that contributes most at each texel. GB outputs the neutral
    // direction constant (0.5) — the gradient pass recomputes it from R afterwards. This eliminates
    // the prior Fill blit (which only wrote the constant rest state), saving one tile flush.
    //
    // Mirrors Grid/DisturbanceStampBatched: up to 32 lights per blit, aspect-corrected radial
    // falloff, (index+1)/16 palette encoding. With _StampCount = 0 the loop is skipped and the
    // output is the pure rest state (identical to what Fill produced).
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

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define MAX_STAMPS 32

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

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;

                // Rest state hardcoded — eliminates the prior Fill blit (which only wrote this
                // constant). R=0: no local boost. GB=0.5: neutral direction. A=0: no palette tag.
                float rRest = 0.0;
                float bestIndex = 0.0;

                float summedBoost = 0.0;
                float bestContribution = 0.0;

                for (int s = 0; s < _StampCount; s++)
                {
                    float2 a = _StampCenters[s].xy;   // segment start (UV)
                    float2 b = _StampCenters[s].zw;   // segment end (UV); == a for a point light
                    float  magnitude = _StampMagnitudes[s];

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
                    float falloff = pow(t, _StampFalloffs[s]);

                    float contribution = falloff * magnitude;
                    summedBoost += contribution;

                    float encoded = _StampColorIndices[s];
                    if (encoded > 0.001 && contribution > bestContribution && falloff > 0.2)
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
    }
}
