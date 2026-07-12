// A sprite whose alpha is eaten away by a scrolling noise mask — the sight reads as if drifting smoke
// keeps covering and uncovering parts of it. Two offset samples of the same noise drift past each other
// for a wispy, non-repeating look. Standard premultiplied sprite pass (see Sprite/SpriteGlitter).
Shader "BalloonParty/Sprite/SightSmoke"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)

        [Header(Smoke Mask)]
        [NoScaleOffset] _NoiseTex("Noise (tileable)", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 3.0
        _ScrollSpeed("Scroll Speed (xy)", Vector) = (0.05, 0.12, 0, 0)
        _SmokeStrength("Smoke Strength", Range(0,1)) = 0.8
        _SmokeContrast("Smoke Contrast", Range(1,8)) = 3.0
        _MinVisibility("Min Visibility", Range(0,1)) = 0.05

        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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

            fixed4 _Color;

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

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _AlphaSplitEnabled;
            sampler2D _NoiseTex;
            float _NoiseScale;
            float4 _ScrollSpeed;
            float _SmokeStrength;
            float _SmokeContrast;
            float _MinVisibility;

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                #if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
                if (_AlphaSplitEnabled)
                    color.a = tex2D(_AlphaTex, uv).r;
                #endif

                return color;
            }

            // Two offset samples of the noise drift past each other so patches never simply repeat;
            // multiplying them deepens the overlaps into wisps. Contrast sharpens the smoke edges,
            // strength dials how much it hides, and the floor keeps the sight faintly readable.
            fixed SmokeMask(float2 uv)
            {
                float2 scroll = _ScrollSpeed.xy * _Time.y;
                float a = tex2D(_NoiseTex, uv * _NoiseScale + scroll).r;
                float b = tex2D(_NoiseTex, uv * _NoiseScale * 1.7 - scroll * 0.6).r;
                float n = a * b;

                float mask = saturate((n - 0.5) * _SmokeContrast + 0.5);
                mask = lerp(1.0, mask, _SmokeStrength);
                return max(mask, _MinVisibility);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.a *= SmokeMask(IN.texcoord);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
