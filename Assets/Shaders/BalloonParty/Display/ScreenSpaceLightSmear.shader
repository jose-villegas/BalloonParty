Shader "Hidden/BalloonParty/Display/ScreenSpaceLightSmear"
{
    // Builds the screen-space light buffer for ScreenSpaceLightService (see
    // PLAN-ScreenSpaceLight.md). Input is the SceneCaptureService capture, whose alpha
    // channel is sprite coverage (the capture camera clears with alpha 0).
    //
    // Pass 0 — directional smear, two opposite marches per pixel (8 taps each, with
    // exponential decay):
    //   rgb (reflection/bleed) marches TOWARD the light — a lit neighbour's color
    //     bleeds onto this pixel, so the glow shows up on the side facing the source.
    //   a (shadow) marches AWAY from the light — an occluder sitting between this
    //     pixel and the source darkens it, so the shadow shows up on the far side.
    // The two must march opposite ways: a shadow is cast onto the side of an object
    // away from the light, while its glow bleeds onto the side facing the light —
    // marching both the same way stacks them on top of each other instead.
    // Pass 1 — 3x3 box soften to remove smear streaks.
    // Pass 2 — temporal blend against the previous smoothed buffer: at capture
    // resolution a moving sprite jumps whole texels per frame and the bounce tint
    // visibly flickers; folding each fresh build in gradually integrates that away
    // (the light is low-frequency, so the lag is invisible).
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
            #include "UnityCG.cginc"

            #define TAP_COUNT 8

            sampler2D _MainTex;
            float4 _TapStepUV;
            float  _TapDecay;
            float  _TapStart;

            fixed4 frag(v2f_img IN) : SV_Target
            {
                float3 bounceAcc = 0;
                float  shadowAcc = 0;
                float  weightSum = 0;

                [unroll]
                for (int t = 0; t < TAP_COUNT; t++)
                {
                    // _TapStart offsets both marches away from the pixel so an object
                    // doesn't fully shadow/glow itself.
                    float offset = _TapStart + t;
                    float w = pow(_TapDecay, t);
                    weightSum += w;

                    float4 lit = tex2D(_MainTex, IN.uv + _TapStepUV.xy * offset);
                    // Premultiplied by coverage: empty sky contributes no bleed.
                    bounceAcc += lit.rgb * lit.a * w;

                    float4 occluder = tex2D(_MainTex, IN.uv - _TapStepUV.xy * offset);
                    shadowAcc += occluder.a * w;
                }

                return float4(bounceAcc / weightSum, shadowAcc / weightSum);
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
