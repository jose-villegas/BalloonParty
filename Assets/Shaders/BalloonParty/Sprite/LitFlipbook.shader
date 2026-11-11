Shader "BalloonParty/Sprite/LitFlipbook"
{
    // Lights baked surface data with the live scene light, so a baked animation (the card reveal's balloon
    // pops, Tools/art/balloon_pop) shades like the board around it. The texture holds the surface, not a
    // picture: red and green the normal's x and y as the camera saw it (0.5 is flat; z is rebuilt), blue
    // the ambient occlusion, alpha the coverage. The light comes from the top of the screen's side the
    // scene light points to (_SceneLightDir), raised by Light Height; the colour is the vertex colour (an
    // Image's or RawImage's colour: the balloon's palette tint).
    //
    // The light's colour and dimming are taken as the balloons' body takes them (Sprite/Diffuse in Ambient
    // mode on Balloon/DiffuseSprite), so a reveal at dusk sits in the same light as the balloons.
    // Works on a RawImage (pick the frame with its UV rect) or a SpriteRenderer.
    Properties
    {
        [PerRendererData] _MainTex ("Surface (normal xy, occlusion, coverage)", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shading)]
        // How far above the screen the light sits: low rakes across the surface, high lights it head on.
        _LightHeight ("Light Height", Range(0.1, 3)) = 0.7
        // The share of the colour a face turned away from the light keeps.
        _Ambient ("Ambient", Range(0, 1)) = 0.38
        // Lets the light wrap past the terminator, as through thin rubber.
        _Wrap ("Wrap", Range(0, 1)) = 0.25
        // How dark the creases and the inside of a curl go: 0 ignores the baked occlusion.
        _Occlusion ("Occlusion", Range(0, 1)) = 0.8
        // Subsurface, as thin latex shows it: light that passes through the rubber, so a face turned away
        // from the light glows in a deeper, richer colour (the tint squared) instead of going dark.
        _Transmission ("Transmission", Range(0, 2)) = 0.9
        // How tightly the glow gathers on the faces turned right away from the light: 1 spreads it over
        // the whole shadow side, higher keeps it to the rim facing away.
        _TransmissionFocus ("Transmission Focus", Range(0.5, 8)) = 1.5
        _Shininess ("Highlight Sharpness", Range(2, 256)) = 48
        _Specular ("Highlight Strength", Range(0, 2)) = 0.55

        [Header(Scene Light)]
        // As on Sprite/Diffuse: how much of the time-of-day light's colour and dimming the surface takes,
        // and how much of its colour it loses as the light darkens. The defaults are the balloons'.
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
                float2 texcoord  : TEXCOORD0;
                float3 lightTint : TEXCOORD1;
                float3 toLight   : TEXCOORD2;
                float3 halfway   : TEXCOORD3;
            };

            sampler2D _MainTex;
            half4 _Color;
            fixed4 _RendererColor;
            float _LightHeight;
            float _Ambient;
            float _Wrap;
            float _Occlusion;
            float _Transmission;
            float _TransmissionFocus;
            float _Shininess;
            float _Specular;
            float _AmbientHue;
            float _AmbientDimming;
            float _AmbientDesaturation;

            // Sprite/Diffuse's: the grey a colour fades to keeps its OKLab lightness.
            float3 SameLightnessGrey(float3 encoded)
            {
                float3 lin = pow(max(encoded, 0.0), 2.2);
                float3 lms = float3(
                    dot(lin, float3(0.4122214708, 0.5363325363, 0.0514459929)),
                    dot(lin, float3(0.2119034982, 0.6806995451, 0.1073969566)),
                    dot(lin, float3(0.0883024619, 0.2817188376, 0.6299787005)));
                float okL = dot(pow(lms, 1.0 / 3.0), float3(0.2104542553, 0.7936177850, -0.0040720468));
                return pow(okL * okL * okL, 1.0 / 2.2).xxx;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                // One light for the whole quad: the flat global one, as the balloons' bodies take it.
                OUT.lightTint = SceneLightTint() + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);
                float dark = 1.0 - saturate(dot(SceneLightTint(), float3(0.2126, 0.7152, 0.0722)));
                OUT.color.rgb = lerp(OUT.color.rgb, SameLightnessGrey(OUT.color.rgb), _AmbientDesaturation * dark);

                // The 2D game camera looks straight down the board, so the screen's axes are the world's.
                OUT.toLight = normalize(float3(SceneLightDirection(), _LightHeight));
                OUT.halfway = normalize(OUT.toLight + float3(0.0, 0.0, 1.0));
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 surface = tex2D(_MainTex, IN.texcoord);
                float2 nxy = surface.rg * 2.0 - 1.0;
                float3 normal = float3(nxy, sqrt(saturate(1.0 - dot(nxy, nxy))));
                float occlusion = 1.0 - _Occlusion * (1.0 - surface.b);

                float diffuse = saturate((dot(normal, IN.toLight) + _Wrap) / (1.0 + _Wrap));
                float highlight = pow(saturate(dot(normal, IN.halfway)), _Shininess) * _Specular * occlusion;

                // Through the rubber: strongest where the face is turned right away from the light, and
                // dimmed in the creases, where the light has more rubber to cross.
                float through = pow(saturate(-dot(normal, IN.toLight)), _TransmissionFocus) * _Transmission * occlusion;

                float3 shade = (_Ambient + (1.0 - _Ambient) * diffuse) * occlusion;
                float3 albedo = IN.color.rgb;
                float3 rgb = (albedo * shade + albedo * albedo * through) * IN.lightTint + IN.lightTint * highlight;
                // Like the balloons: however hot the light, the rubber keeps its colour and never blooms.
                rgb = SceneLightCapToWhite(rgb);

                float alpha = surface.a * IN.color.a;
                return half4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
