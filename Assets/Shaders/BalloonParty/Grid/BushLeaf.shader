Shader "BalloonParty/Grid/BushLeaf"
{
    Properties
    {
        _MainTex ("Leaf Atlas", 2D) = "white" {}

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)           = (0.15, 0.18, 0.1, 0.55)
        _ShadowOffset   ("Offset",   Vector)          = (0.04, -0.06, 0, 0)
        _ShadowSoftness ("Softness", Range(0, 0.08))  = 0.015
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _ShadowColor;
            float2 _ShadowOffset;
            float  _ShadowSoftness;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(fixed4, _LeafTint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UVRect)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rawUV : TEXCOORD1;
                float4 uvRect : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);

                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props, _UVRect);
                o.uv = rect.xy + v.uv * rect.zw;
                o.rawUV = v.uv;
                o.uvRect = rect;

                return o;
            }

            inline float2 RemapUV(float2 rawUV, float4 rect)
            {
                return rect.xy + rawUV * rect.zw;
            }

            inline fixed SampleShadowAlpha(float2 rawUV, float4 rect)
            {
                float2 uv = RemapUV(rawUV, rect);
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                float2 shadowRaw = i.rawUV + _ShadowOffset;
                float s = _ShadowSoftness;

                fixed shadowAlpha;
                if (s > 0.001)
                {
                    shadowAlpha = (
                        SampleShadowAlpha(shadowRaw + float2(-s, -s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( 0, -s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( s, -s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2(-s,  0), i.uvRect) +
                        SampleShadowAlpha(shadowRaw,                  i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( s,  0), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2(-s,  s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( 0,  s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( s,  s), i.uvRect)
                    ) / 9.0;
                }
                else
                {
                    shadowAlpha = SampleShadowAlpha(shadowRaw, i.uvRect);
                }

                fixed4 shadow = fixed4(_ShadowColor.rgb, _ShadowColor.a * shadowAlpha);

                fixed4 col = tex2D(_MainTex, i.uv);
                fixed4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _LeafTint);
                col *= tint;

                // Composite: shadow behind, leaf on top (Porter-Duff "over")
                fixed3 rgb = col.rgb * col.a + shadow.rgb * shadow.a * (1.0 - col.a);
                fixed  a   = col.a + shadow.a * (1.0 - col.a);
                return fixed4(rgb / max(a, 0.001), a);
            }
            ENDCG
        }
    }
}

