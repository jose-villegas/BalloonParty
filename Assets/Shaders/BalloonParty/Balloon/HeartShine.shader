Shader "BalloonParty/Balloon/HeartShine"
{
    // The heart balloon's specular. The round balloons' shine is an arc drawn for a circle and swung round
    // the balloon's centre, which a heart's outline doesn't follow. Here the sprite is the heart's own
    // normals (baked from the same height map that shades its body, by Tools/art/make_heart_balloon.py), and
    // the highlight is lit from them toward the scene light, so it lands on each lobe and on the rise
    // below the cleft wherever the surface faces the light. It answers the light as the round balloons'
    // shine material does: the same global direction, the light's colour, the night re-grade, and the
    // opacity ramp by the light's magnitude, so it dims at night with theirs.
    Properties
    {
        [PerRendererData] _MainTex ("Normals (RGB), Heart (A)", 2D) = "bump" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Light)]
        // How high the light stands over the board: lower slides the highlight toward the lit rim.
        _LightHeight ("Light Height", Range(0.2, 3)) = 0.7

        [Header(Highlight)]
        // A broad soft sheen and, inside it, a crisp core with a clean edge, the cartoon gloss of the
        // round balloons' arc.
        _GlowPower ("Glow Sharpness", Range(2, 128)) = 12
        _GlowIntensity ("Glow Intensity", Range(0, 1)) = 0.45
        _CorePower ("Core Sharpness", Range(8, 400)) = 90
        _CoreThreshold ("Core Threshold", Range(0, 1)) = 0.6
        _CoreSoftness ("Core Edge Softness", Range(0.01, 0.5)) = 0.12

        [Header(Light Response)]
        _AmbientHue ("Ambient Hue", Range(0, 1)) = 0.25
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 0.4
        _AlphaFullAt ("Alpha Full-Light Level", Range(0.1, 3)) = 1.73
        _AlphaFalloff ("Alpha Falloff", Range(0.25, 4)) = 0.25

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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

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
                float3 halfway   : TEXCOORD1;
                float3 lightTint : TEXCOORD2;
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
            float _LightHeight;
            float _GlowPower;
            float _GlowIntensity;
            float _CorePower;
            float _CoreThreshold;
            float _CoreSoftness;
            float _AmbientHue;
            float _AmbientDimming;
            float _AlphaFullAt;
            float _AlphaFalloff;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;

                // The normals are in the sprite's own space, which turns as the balloon sways; the light is
                // turned into that space, so the highlight stays put on the board while the heart rocks under it.
                float2 worldX   = float2(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10);
                float  objAngle = dot(worldX, worldX) > 1e-8 ? atan2(worldX.y, worldX.x) : 0.0;
                float  s = sin(-objAngle);
                float  c = cos(-objAngle);
                float2 toLight2D = SceneLightDirection();
                toLight2D = float2(toLight2D.x * c - toLight2D.y * s, toLight2D.x * s + toLight2D.y * c);
                float3 toLight = normalize(float3(toLight2D, _LightHeight));
                OUT.halfway = normalize(toLight + float3(0, 0, 1));

                float2 worldCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                OUT.lightTint = SceneLightTintAtLOD(worldCenter) + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);

#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4  sprite = tex2D(_MainTex, IN.texcoord);
                float3 normal = normalize(sprite.rgb * 2.0 - 1.0);
                float  facing = saturate(dot(normal, normalize(IN.halfway)));

                float glow = pow(facing, _GlowPower) * _GlowIntensity;
                float core = saturate((pow(facing, _CorePower) - _CoreThreshold) / _CoreSoftness);
                float spec = saturate(glow + core);

                half4 col;
                col.rgb = SceneLightCapToWhite(IN.color.rgb * IN.lightTint);
                float lit = saturate(length(IN.lightTint) / _AlphaFullAt);
                col.a = spec * sprite.a * IN.color.a * pow(lit, _AlphaFalloff);
                col.rgb *= col.a;
                return col;
            }
            ENDCG
        }
    }
}
