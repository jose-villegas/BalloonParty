Shader "Hidden/BalloonParty/Display/ScreenSpaceLightSmear"
{
    // Builds the screen-space light buffer for ScreenSpaceLightService (see the
    // arch_screen_space_light doc). Input is the SceneCaptureService capture, whose alpha
    // channel is sprite coverage (the capture camera clears with alpha 0).
    //
    // Pass 0 — directional smear, two opposite marches per pixel (8 taps each, with
    // exponential decay). The march direction is PER-FRAGMENT: each pixel maps its
    // capture UV to world through the field bounds and reads the local toward-light
    // direction from the light field (SceneLight.cginc), so a point/area light bends
    // the bleed and shadows around it. Field-off the field returns the flat global
    // direction everywhere and this reduces to the old single-direction smear.
    //   rgb (reflection/bleed) marches DOWN-light — the composited scene color
    //     down-light of this pixel (sky included, not premultiplied by coverage); the
    //     overlay subtracts the ambient sky so only bright/dark deviations bleed.
    //   a (shadow) marches TOWARD the light — an occluder sitting between this pixel
    //     and the source darkens it, so the shadow shows up on the far side.
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
            #include "../Include/SceneLight.cginc"

            #define TAP_COUNT 8

            sampler2D _MainTex;
            float  _TapStepScale;
            float  _TapAspect;
            float  _TapDecay;
            float  _TapStart;

            fixed4 frag(v2f_img IN) : SV_Target
            {
                float3 bounceAcc = 0;
                float  shadowAcc = 0;
                float  weightSum = 0;

                // Coverage at this pixel — casters have ~1, open ground/sky ~0.
                float ownCoverage = tex2D(_MainTex, IN.uv).a;

                // Per-fragment march direction from the light field: map this pixel's capture
                // UV to world through the field bounds, read the toward-light direction there,
                // and march DOWN-light (negated). The unit direction becomes a UV step via the
                // service-pushed world→UV scale, with the aspect correcting X for a non-square
                // view. Field-off, the helper returns the flat global direction everywhere and
                // the scale/aspect are uniform, so stepUv collapses to the single global step
                // the service used to push — bit-identical to the old fixed march.
                float2 worldPos = _SceneLightFieldBoundsMin.xy + IN.uv * _SceneLightFieldBoundsSize.xy;
                float2 downLight = -SceneLightDirectionAt(worldPos);
                float2 stepUv = float2(downLight.x / _TapAspect, downLight.y) * _TapStepScale;

                [unroll]
                for (int t = 0; t < TAP_COUNT; t++)
                {
                    // _TapStart offsets both marches away from the pixel so an object
                    // doesn't fully shadow/glow itself.
                    float offset = _TapStart + t;
                    float w = pow(_TapDecay, t);
                    weightSum += w;

                    // Bounce = the composited scene color down-light (sprites are already
                    // blended over the sky clear in the capture, so this is "what the scene
                    // looks like down-light of here"). NOT premultiplied by coverage —
                    // premultiplying zeroed the sky to black, which both read wrong in the
                    // buffer and, with nothing to dilute it, dumped a lone nearby sprite's
                    // full color onto its neighbours. The overlay subtracts the ambient sky
                    // so flat areas net to neutral; deviations (bright sprite / dark sprite)
                    // are what actually bleed. Shadow marches the opposite way (toward the
                    // light): an occluder between here and the source darkens the far side.
                    float4 lit = tex2D(_MainTex, IN.uv + stepUv * offset);
                    bounceAcc += lit.rgb * w;

                    float4 occluder = tex2D(_MainTex, IN.uv - stepUv * offset);
                    shadowAcc += occluder.a * w;
                }

                // Cast the shadow only onto NON-occluder pixels: a caster sampling its own
                // coverage would otherwise just darken itself into a centered blob rather
                // than throwing a shadow onto the ground beside it. (1 - ownCoverage)
                // masks the casters out, leaving the offset silhouette on open ground.
                float shadow = (shadowAcc / weightSum) * (1.0 - ownCoverage);

                return float4(bounceAcc / weightSum, shadow);
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
