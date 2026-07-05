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
        // The ambient sky color the bounce is measured against — pushed by the service
        // from the camera background, so flat sky nets to neutral (no global tint).
        _AmbientColor   ("Ambient Color",   Color)       = (0.6, 0.8, 0.95, 1)
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
            fixed4 _AmbientColor;

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

                // Bounce is the scene color up-light measured against the ambient sky:
                // flat sky (bounce == ambient) contributes zero, a bright sprite pushes
                // positive (brightens neighbours in its hue), a dark/black sprite pushes
                // negative (absorbs — darkens neighbours). 0.5 * tint is blend-neutral.
                fixed3 bounce = (gathered.rgb - _AmbientColor.rgb) * _BounceStrength;
                fixed3 color = shadowTint * 0.5 + bounce;

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
