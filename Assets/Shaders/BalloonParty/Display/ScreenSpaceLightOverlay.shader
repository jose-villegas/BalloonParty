Shader "BalloonParty/Display/ScreenSpaceLightOverlay"
{
    // Fullscreen composite for ScreenSpaceLightService (see PLAN-ScreenSpaceLight.md).
    // Rendered as a camera-fitted quad ABOVE all gameplay — NOT a post effect: the
    // multiplicative blend (2*src*dst, 0.5 neutral) tints the frame in place with no
    // framebuffer readback, so tile GPUs never stall. Samples only the low-res light
    // buffer built by ScreenSpaceLightSmear: a = shadow amount, rgb = bounce color.
    Properties
    {
        [NoScaleOffset] _LightTex ("Light Buffer", 2D) = "gray" {}
        _ShadowTint     ("Shadow Tint",     Color)       = (0.55, 0.6, 0.75, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
        _BounceStrength ("Bounce Strength", Range(0, 2)) = 0.25
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
        Blend DstColor SrcColor

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _LightTex;
            fixed4 _ShadowTint;
            float  _ShadowStrength;
            float  _BounceStrength;

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

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 gathered = tex2D(_LightTex, IN.texcoord);

                float shadow = saturate(gathered.a * _ShadowStrength);
                fixed3 shadowTint = lerp(fixed3(1, 1, 1), _ShadowTint.rgb, shadow);

                // 0.5 * tint is blend-neutral when unshadowed; bounce pushes above
                // neutral, brightening — bounce light also fills shadows.
                fixed3 color = shadowTint * 0.5 + gathered.rgb * _BounceStrength;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
