Shader "BalloonParty/Sprite/Diffuse"
{
    // The diffuse term of the scene light for ordinary sprites: renders the sprite multiplied
    // by the global light's colour × intensity (albedo × light) — the piece that makes a
    // tinted/dimmed scene light read on surfaces, not just on speculars and shines.
    //
    // Light Influence blends between unlit (0) and fully lit (1): full multiplication sends
    // everything black at intensity 0 and can fight gameplay colour readability, so gameplay
    // sprites often want a partial influence while scenery can run at 1.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Light Response)]
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
        // Full   — the field: global ambient + any local point/area lights (default).
        // Ambient — the global scene light only (colour × intensity), skips the field texture entirely.
        // Local  — only local field lights, ABOVE the ambient rest: neutral until a light is near, then
        //          it brightens/tints toward that light. Never picks up the global ambient.
        [Enum(Full, 0, Ambient, 1, Local, 2)] _LightMode ("Light Mode", Float) = 0
        // How much of the time-of-day light's COLOUR the sprite takes: 1 = all of it, 0 = only its
        // brightness. Gameplay colours keep their hue at night this way (a red balloon under the
        // blue midnight light would otherwise turn maroon). Local lights keep their colour.
        _AmbientHue ("Ambient Hue", Range(0, 1)) = 1
        // How much of the time-of-day light's DIMMING the sprite takes: 1 = all of it, 0 = none, as if
        // a key light stayed on it. Leaves the backdrop to carry the night.
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 1
        // How much of its colour the sprite loses as the time-of-day light darkens: 0 = none, 1 = grey
        // under a black light. Colour fades with light (the Hunt effect), so a balloon kept bright at
        // night by the two settings above would otherwise look more vivid than the night around it.
        _AmbientDesaturation ("Ambient Desaturation", Range(0, 1)) = 0

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                // Sampled ONCE per sprite (its own centre, vertex stage) — the PaintBlob pattern.
                float3 lightTint : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            sampler2D _MainTex;
            half4 _Color;
            float _LightInfluence;
            float _LightMode;
            float _AmbientHue;
            float _AmbientDimming;
            float _AmbientDesaturation;

            // The grey a colour fades to keeps its OKLab lightness. A luma grey would not: it turns
            // saturated red and purple muddy and green pale, since they look lighter than their luma.
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
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                // The whole sprite is lit from one reading at its centre (VTF, target 3.5). The mode is a
                // material constant, so the branch is uniform (no divergence).
                float2 spriteCenterWorld = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
                if (_LightMode > 1.5)
                {
                    // Local: only nearby field lights, no ambient — neutral (1) until a light is near, then
                    // its colour × boost adds on top. The frag's lerp from white keeps rest = unlit.
                    OUT.lightTint = float3(1.0, 1.0, 1.0) + SceneLightLocalAtLOD(spriteCenterWorld);
                }
                else if (_LightMode > 0.5)
                {
                    // Ambient: the flat global light, no field texture read at all.
                    OUT.lightTint = SceneLightTint();
                }
                else
                {
                    // Full: the field — ambient + any local lights.
                    OUT.lightTint = SceneLightTintAtLOD(spriteCenterWorld);
                }

                if (_LightMode < 1.5)
                {
                    // Re-grade only the time-of-day part of the light; whatever a local light adds stays.
                    float3 ambient = SceneLightTint();
                    float3 graded = lerp(dot(ambient, float3(0.2126, 0.7152, 0.0722)).xxx, ambient, _AmbientHue);
                    graded = lerp(float3(1.0, 1.0, 1.0), graded, _AmbientDimming);
                    OUT.lightTint += graded - ambient;

                    // The vertex colour carries the sprite's colour (a balloon's palette tint), so fading it
                    // here costs nothing per pixel. Full at noon, where the ambient's brightness is 1.
                    float dark = 1.0 - saturate(dot(ambient, float3(0.2126, 0.7152, 0.0722)));
                    OUT.color.rgb = lerp(OUT.color.rgb, SameLightnessGrey(OUT.color.rgb), _AmbientDesaturation * dark);
                }

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // albedo × light, eased by influence so gameplay colours stay readable.
                c.rgb *= lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence);
                // A pop's light can take a balloon several times past white; it stays its colour and never blooms.
                c.rgb = SceneLightCapToWhite(c.rgb);

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
