Shader "BalloonParty/Sprite/CelRibbon"
{
    // The card reveal's ribbons (UI/CardReveal/RibbonGraphic), cel shaded in the scene's light. The mesh carries
    // no texture, only what the shading needs in its first UV: x is how far the ribbon's fold at that vertex
    // turns to the right (-1 left, 1 right, from the wave's slope: the ribbon twisting along its length), y is 1
    // along the ribbon's middle and 0 at its edges, and -1 marks the copy of the strip that draws its drop
    // shadow, which this shader moves away from the light.
    // A fold facing the light is drawn in the ribbon's colour, one facing away in the shadow tone, with a hard
    // step between, and a satin sheen runs along the middle of the lit folds. The colour takes the time-of-day
    // light as the balloons' bodies do (Sprite/Diffuse in Ambient mode). For a canvas: the batch's object space
    // is the canvas's, so the shadow's distance is in canvas units. Not mask-safe: it has no UI clip rect or
    // stencil, so under a RectMask2D or Mask it draws unclipped (PencilText's block is the one to copy).
    Properties
    {
        [PerRendererData] _MainTex ("Unused", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Cel Shading)]
        // How much the light has to face a fold to light it: 0 splits lit and shadowed folds evenly.
        _LitEdge ("Lit Edge", Range(-1, 1)) = -0.1
        _ShadowTone ("Shadow Tone", Range(0, 1)) = 0.74
        _BandSoftness ("Band Softness", Range(0.001, 0.3)) = 0.04
        // The sheen along the lit folds' middle: how much of the ribbon's width it covers and how bright.
        _SheenWidth ("Sheen Width", Range(0, 1)) = 0.22
        _Sheen ("Sheen", Range(0, 1)) = 0.35

        [Header(Drop Shadow)]
        _ShadowColor ("Color", Color) = (0.12, 0.1, 0.14, 0.28)
        // How far the shadow falls away from the light, in canvas units.
        _ShadowReach ("Distance", Range(0, 12)) = 3

        [Header(Scene Light)]
        _AmbientHue ("Ambient Hue", Range(0, 1)) = 0.25
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 0.4
        _AmbientDesaturation ("Ambient Desaturation", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        ZTest    [unity_GUIZTestMode]
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 shading   : TEXCOORD0;
                float3 lightTint : TEXCOORD1;
                float  lightSide : TEXCOORD2;
                float  isShadow  : TEXCOORD3;
            };

            half4 _Color;
            float _LitEdge;
            float _ShadowTone;
            float _BandSoftness;
            float _SheenWidth;
            float _Sheen;
            fixed4 _ShadowColor;
            float _ShadowReach;
            float _AmbientHue;
            float _AmbientDimming;
            float _AmbientDesaturation;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                float2 toLight = SceneLightDirection();
                OUT.isShadow = IN.texcoord.y < -0.5 ? 1.0 : 0.0;
                float4 position = IN.vertex;
                position.xy -= toLight * _ShadowReach * OUT.isShadow;
                OUT.vertex = UnityObjectToClipPos(position);
                OUT.color = IN.color * _Color;
                OUT.color.rgb = SceneLightDesaturate(OUT.color.rgb, _AmbientDesaturation);
                OUT.shading = IN.texcoord;
                OUT.lightTint = SceneLightTint() + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);

                // A fold turns only left or right (the ribbon twisting along its length), so only the light's side lights it.
                OUT.lightSide = toLight.x;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float alpha = IN.color.a;
                if (IN.isShadow > 0.5)
                {
                    float shadow = _ShadowColor.a * alpha * ShadowLightFade();
                    return half4(_ShadowColor.rgb * shadow, shadow);
                }

                float facing = IN.shading.x * IN.lightSide;
                float lightness = smoothstep(_LitEdge - _BandSoftness, _LitEdge + _BandSoftness, facing);
                float tone = lerp(_ShadowTone, 1.0, lightness);
                float sheen = smoothstep(1.0 - _SheenWidth - _BandSoftness, 1.0 - _SheenWidth + _BandSoftness,
                    IN.shading.y) * lightness * _Sheen;
                float3 rgb = saturate(IN.color.rgb * tone + sheen) * IN.lightTint;
                rgb = SceneLightCapToWhite(rgb);
                return half4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
