Shader "Hidden/BalloonParty/SceneLightAccumulate"
{
    // Pass 2 of the light-field chain (see @ref plan_lighting "Milestone 3"): reads the rest-filled
    // field and ADDS every registered light's magnitude into R, tagging A with the palette index of the
    // light that contributes most at each texel. GB (the rest direction) is passed through untouched —
    // the gradient pass recomputes it from R afterwards.
    //
    // Mirrors Grid/DisturbanceStampBatched: up to 32 lights per blit, aspect-corrected radial
    // falloff, (index+1)/16 palette encoding. With _StampCount = 0 the loop is skipped and the pass
    // is an exact identity on R, GB and A (the rest field survives bit-for-bit).
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

            sampler2D _MainTex;
            float     _MaxBoost;    // ceiling on the summed additive boost above rest (soft-clamped)
            float     _StampAspect; // field height/width; corrects UV anisotropy so stamps stay circular
            int       _StampCount;

            float  _FalloffPower;                  // radial falloff exponent (1 = linear cone)
            float4 _StampCenters[MAX_STAMPS];      // xy = UV center
            float  _StampRadii[MAX_STAMPS];        // UV radius
            float  _StampMagnitudes[MAX_STAMPS];   // peak magnitude at the centre (>= 0)
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
                float4 current = tex2D(_MainTex, uv);

                float rRest = current.r;
                // Palette index is written hard (dominant source wins), never blended — averaging
                // two indices would decode to a wrong color. Default: keep whatever's there (rest = 0).
                float bestIndex = current.a;

                float summedBoost = 0.0;
                float bestContribution = 0.0;

                for (int s = 0; s < _StampCount; s++)
                {
                    float2 center = _StampCenters[s].xy;
                    float  radius = _StampRadii[s];
                    float  magnitude = _StampMagnitudes[s];

                    // Correct the per-axis UV normalisation so the falloff is circular in world space.
                    float2 toPixel = uv - center;
                    toPixel.y *= _StampAspect;
                    float dist = length(toPixel);

                    // Smooth radial falloff, peak at the centre → 0 at the radius, shaped by _FalloffPower
                    // (1 = linear cone, higher = more concentrated). No plateau, so R (and the direction
                    // the gradient pass derives from it) varies continuously across the whole disc.
                    float t = saturate(1.0 - dist / max(radius, 1e-4));
                    float falloff = pow(t, _FalloffPower);

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

                return float4(rRest + boost, current.g, current.b, bestIndex);
            }
            ENDCG
        }
    }
}
