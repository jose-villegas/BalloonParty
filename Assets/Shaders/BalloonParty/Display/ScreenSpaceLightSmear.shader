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
    //     Skipped (branched) on fragments inside a sprite's own coverage, since the result
    //     is zeroed by (1 - ownCoverage) there anyway.
    //   BOUNCE — 4 directions at 90° spacing around the field's local toward-light vector
    //     (8 taps each, _MipSpread cone widening). Each direction gathers indirect color
    //     from lit surfaces at that angle — omnidirectional color bleed. The primary
    //     direction (down-light) has weight 1; the three secondary directions are scaled
    //     by _SecondaryWeight (0 = single-direction, 1 = equal). _SecondaryWeight = 0
    //     collapses to the old single-direction march, bit-identical to the pre-RSM shader.
    //   The march direction is PER-FRAGMENT from the light field (SceneLight.cginc), so
    //   local lights bend all four directions around them.
    // Pass 1 — 3x3 box soften to remove smear streaks.
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
            #include "../Include/BackgroundField.cginc"

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
            float  _CloudGateStrength;

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
                // Skipped entirely inside sprites (ownCoverage ~= 1): the result is multiplied
                // by (1 - ownCoverage) below anyway, so the 8 taps would just compute a value
                // that gets zeroed. The branch is spatially coherent (whole sprite interiors on
                // a busy screen), so it's cheap on mobile. The 0.999 threshold bounds the skipped
                // contribution to <= 0.001 — below what the ARGB32 buffer resolves.
                float shadow = 0;

                [branch]
                if (ownCoverage < 0.999)
                {
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

                    shadow = (shadowAcc / shadowWeightSum) * (1.0 - ownCoverage);

                    // Gate the shadow by the cloud field: the backdrop cloud is the surface shadows land
                    // on, so don't smear shadow onto no-cloud ground. _CloudGateStrength dials how strongly
                    // (1 = full, 0 = ignore cloud). No-op when the cloud field is absent.
                    shadow *= lerp(1.0, BackgroundFieldGate(worldPos), _CloudGateStrength);
                }

                // --- Bounce: 4 directions (primary + 3 secondary at 90° spacing) ---
                float2 dirs[4] = {
                    stepBase,                                // 0°   (primary, down-light)
                    float2(-stepBase.y, stepBase.x),         // +90°
                    -stepBase,                               // 180° (up-light)
                    float2(stepBase.y, -stepBase.x)          // -90°
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
    }
}
