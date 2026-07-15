Shader "Hidden/BalloonParty/Display/ScreenSpaceLightSmear"
{
    // Builds the screen-space light buffer for ScreenSpaceLightService (see the
    // arch_screen_space_light doc). Input is the SceneCaptureService capture (with mipmaps),
    // whose alpha channel is sprite coverage (the capture camera clears with alpha 0).
    //
    // Pass 0 — multi-direction cone march (RSM-style VPL gather in 2D). Shadow and bounce
    // are decoupled:
    //   SHADOW — single direction toward the light (8 taps, _ShadowMipSpread penumbra).
    //     Directional by definition: an occluder blocks light between source and receiver.
    //   BOUNCE — 4 directions at 90° spacing around the field's local toward-light vector
    //     (8 taps each, _MipSpread cone widening). Each direction gathers indirect color
    //     from lit surfaces at that angle — omnidirectional color bleed. The primary
    //     direction (down-light) has weight 1; the three secondary directions are scaled
    //     by _SecondaryWeight (0 = single-direction, 1 = equal). _SecondaryWeight = 0
    //     collapses to the old single-direction march, bit-identical to the pre-RSM shader.
    //   The march direction is PER-FRAGMENT from the light field (SceneLight.cginc), so
    //   local lights bend all four directions around them.
    // Pass 1 — 3x3 box soften to remove smear streaks.
    // Pass 2 — temporal blend against the previous smoothed buffer.
    Properties
    {
        _MainTex ("Source", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            #define TAP_COUNT 8

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float  _TapStepScale;
            float  _TapAspect;
            float  _TapDecay;
            float  _TapStart;
            float  _MipSpread;
            float  _ShadowMipSpread;
            float  _SecondaryWeight;
            float  _BounceJitter;

            // Rotate a 2D vector by (cos, sin) pair.
            float2 rot(float2 v, float2 cs) { return float2(v.x*cs.x - v.y*cs.y, v.x*cs.y + v.y*cs.x); }

            fixed4 frag(v2f_img IN) : SV_Target
            {
                // Coverage at this pixel — casters have ~1, open ground/sky ~0.
                float ownCoverage = tex2Dlod(_MainTex, float4(IN.uv, 0, 0)).a;

                // Per-fragment march direction from the light field.
                float2 worldPos = _SceneLightFieldBoundsMin.xy + IN.uv * _SceneLightFieldBoundsSize.xy;
                float2 downLight = -SceneLightDirectionAt(worldPos);
                float2 stepBase = float2(downLight.x / _TapAspect, downLight.y) * _TapStepScale;

                // Mip count for clamping.
                float maxMip = log2(max(_MainTex_TexelSize.z, _MainTex_TexelSize.w));

                // --- Shadow: single direction (toward the light) ---
                float shadowAcc = 0;
                float shadowWeightSum = 0;

                [unroll]
                for (int s = 0; s < TAP_COUNT; s++)
                {
                    float offset = _TapStart + s;
                    float w = pow(_TapDecay, s);
                    float shadowMip = min(_ShadowMipSpread * log2(1.0 + (float)s), maxMip);
                    float4 occluder = tex2Dlod(_MainTex, float4(IN.uv - stepBase * offset, 0, shadowMip));
                    shadowAcc += occluder.a * w;
                    shadowWeightSum += w;
                }

                float shadow = (shadowAcc / shadowWeightSum) * (1.0 - ownCoverage);

                // --- Bounce: 4 directions (primary + 3 secondary at 90° spacing) ---
                // Temporal jitter rotates the entire fan by _BounceJitter radians each frame
                // so that the temporal EMA integrates more unique angles over time.
                float2 jitterCS;
                sincos(_BounceJitter, jitterCS.y, jitterCS.x);
                float2 jitteredBase = rot(stepBase, jitterCS);

                float2 dirs[4] = {
                    jitteredBase,                                    // 0°   (primary, down-light)
                    float2(-jitteredBase.y, jitteredBase.x),         // +90°
                    -jitteredBase,                                   // 180° (up-light)
                    float2(jitteredBase.y, -jitteredBase.x)          // -90°
                };
                float dirWeights[4] = { 1.0, _SecondaryWeight, _SecondaryWeight, _SecondaryWeight };

                float3 bounceAcc = 0;
                float  bounceWeightSum = 0;

                [unroll]
                for (int d = 0; d < 4; d++)
                {
                    float dw = dirWeights[d];
                    if (dw < 0.001) continue;

                    [unroll]
                    for (int t = 0; t < TAP_COUNT; t++)
                    {
                        float offset = _TapStart + t;
                        float w = pow(_TapDecay, t) * dw;
                        float mip = min(_MipSpread * log2(1.0 + (float)t), maxMip);
                        float4 lit = tex2Dlod(_MainTex, float4(IN.uv + dirs[d] * offset, 0, mip));
                        bounceAcc += lit.rgb * w;
                        bounceWeightSum += w;
                    }
                }

                return float4(bounceAcc / bounceWeightSum, shadow);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            fixed4 frag(v2f_img IN) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float4 acc = 0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        acc += tex2D(_MainTex, IN.uv + texel * float2(x, y));
                    }
                }

                return acc / 9.0;
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _HistoryTex;
            float _TemporalBlend;

            fixed4 frag(v2f_img IN) : SV_Target
            {
                fixed4 current = tex2D(_MainTex, IN.uv);
                fixed4 history = tex2D(_HistoryTex, IN.uv);
                return lerp(history, current, _TemporalBlend);
            }
            ENDCG
        }
    }
}
