Shader "BalloonParty/Display/EffectsMapComposite"
{
    // Lays an effects map back over the frame (EffectsMapCompositor, PLAN-EffectsLayerClearing), seen through a
    // dome of thick glass over each ball where the clearing mask is open. The map holds the effects
    // premultiplied, its alpha their coverage, so with the mask empty this is the same blend the effects would
    // have made drawing straight into the frame.
    //
    // A stamp's profile, 1 - (d/r)^2, is a dome's height, so the mask's slope is the glass's surface: the effects
    // refract toward the dome's middle (magnified, so they spread away from the ball and pile up under the rim),
    // split a little by colour at the steep rim, and fade under the glass by the clearing strength. A glint on
    // the dome is added light, so it shows over the board too.
    Properties
    {
        [NoScaleOffset] _EffectsMapTex ("Effects Map", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _EffectsMapTex;

            // Globals, from EffectsClearingMaskView: the mask (red how cleared, green the dome's radius in view
            // heights); (edge low, edge high, strength, view aspect); and (refraction as a fraction of the radius,
            // one mask texel in view heights, dispersion, highlight).
            sampler2D_half _EffectsClearMask;
            float4 _EffectsClearParams;
            float4 _EffectsClearLens;

            // The glint's light: up and to the left of the screen, leaning toward the viewer.
            static const float3 GlintLight = float3(-0.45, 0.6, 0.66);

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            float2 Mask(float2 uv)
            {
                return tex2Dlod(_EffectsClearMask, float4(uv, 0, 0)).rg;
            }

            fixed4 Effects(float2 uv)
            {
                return tex2Dlod(_EffectsMapTex, float4(uv, 0, 0));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float2 centre = Mask(uv);
                float mask = centre.x;

                // Most of the screen, most of the time: no glass, the plain map.
                if (mask <= 0.0)
                {
                    return Effects(uv);
                }

                float aspect = _EffectsClearParams.w;

                // The dome's slope, in heights per radius: 1 at the rim of a fresh stamp, 0 on top. Taken a few
                // mask texels apart, so the mask's steps don't show as ripples, but within the dome. Its radius is
                // the widest the taps see: at the rim the filter blends it with the empty mask outside.
                float step = max(centre.y * 0.12, 1.5 * _EffectsClearLens.y);
                float2 offset = float2(step / aspect, step);
                float2 right = Mask(uv + float2(offset.x, 0));
                float2 left = Mask(uv - float2(offset.x, 0));
                float2 up = Mask(uv + float2(0, offset.y));
                float2 down = Mask(uv - float2(0, offset.y));
                float radius = max(max(centre.y, max(right.y, left.y)), max(up.y, down.y));
                float2 slope = float2(right.x - left.x, up.x - down.x) * (radius / (4.0 * step));

                // Toward the top of the dome, so what lies under it reads magnified and pushed out from the ball;
                // by a share of its radius, so a small dome and a big one are the same glass. Eased in over the
                // glass's outer edge, so what the rim bends meets what lies outside it without a tear.
                float2 bend = slope * (_EffectsClearLens.x * radius) * smoothstep(0.0, 0.12, mask);
                bend.x /= aspect;
                float spread = _EffectsClearLens.z;
                fixed4 red = Effects(uv + bend * (1.0 + spread));
                fixed4 green = Effects(uv + bend);
                fixed4 blue = Effects(uv + bend * (1.0 - spread));
                fixed4 effects = fixed4(red.r, green.g, blue.b, (red.a + green.a + blue.a) / 3.0);

                half fade = 1.0 - _EffectsClearParams.z
                    * smoothstep(_EffectsClearParams.x, _EffectsClearParams.y, mask);

                // The dome's normal leans out from the ball where it slopes; the glint is its mirror highlight
                // plus a thin bright rim where the glass is steepest.
                float glass = smoothstep(0.0, 0.04, mask);
                float3 normal = normalize(float3(-slope, 0.7));
                float mirror = pow(saturate(dot(reflect(float3(0, 0, -1), normal), GlintLight)), 24.0);
                float rim = pow(saturate(length(slope)), 4.0) * 0.35;
                half glint = (mirror + rim) * _EffectsClearLens.w * glass;

                fixed4 result = effects * fade;
                result.rgb += glint;
                return result;
            }
            ENDCG
        }
    }
}
