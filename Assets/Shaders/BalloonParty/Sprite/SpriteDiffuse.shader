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
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
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
            fixed4 _Color;
            float _LightInfluence;

            // Global shader properties — set by SceneLightService, not in Properties so
            // material values can't mask them. Colour's alpha is the "owner has pushed"
            // validity flag (see SceneLightTint).
            float4 _SceneLightColor;
            float  _SceneLightIntensity;

            // The light's colour × intensity — the diffuse multiplier. Neutral (white) when
            // the owner hasn't pushed yet, so nothing dims at edit time.
            float3 SceneLightTint()
            {
                return _SceneLightColor.a > 0.5
                    ? _SceneLightColor.rgb * _SceneLightIntensity
                    : float3(1.0, 1.0, 1.0);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // albedo × light, eased by influence so gameplay colours stay readable.
                c.rgb *= lerp(float3(1.0, 1.0, 1.0), SceneLightTint(), _LightInfluence);

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
