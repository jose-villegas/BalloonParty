Shader "BalloonParty/Sprite/Blur"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // How many texels each blur step reaches. Higher = softer/wider.
        _BlurAmount ("Blur Amount (pixels)", Range(0, 16)) = 2.0

        // Scales the sprite content down to leave transparent padding on each side,
        // so the blur can expand outward without hard-clipping at the sprite boundary.
        // 1.0 = clips at edges; 0.9 = 5% transparent margin on all sides.
        _SpriteScale ("Sprite Scale", Range(0.5, 1.0)) = 0.9
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
            #pragma target 2.5
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            fixed4    _Color;
            float     _BlurAmount;
            float     _SpriteScale;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            // Remap quad UV so the sprite content occupies only the inner _SpriteScale
            // portion. The outer margin remains transparent, giving blur room to bleed
            // outward without clipping against the sprite quad boundary.
            float2 ScaleUV(float2 uv)
            {
                float margin = (1.0 - _SpriteScale) * 0.5;
                return (uv - margin) / _SpriteScale;
            }

            // 9-tap uniform box blur in texel space.
            // half4 accumulation — fixed is 8-bit on many mobile GPUs and loses
            // precision when summing 9 samples.
            half4 SampleBlurred(float2 tc)
            {
                float2 o = _MainTex_TexelSize.xy * _BlurAmount;

                half4 col = half4(0, 0, 0, 0);
                col += tex2D(_MainTex, tc + float2(-o.x, -o.y));
                col += tex2D(_MainTex, tc + float2(   0, -o.y));
                col += tex2D(_MainTex, tc + float2( o.x, -o.y));
                col += tex2D(_MainTex, tc + float2(-o.x,    0));
                col += tex2D(_MainTex, tc                     );
                col += tex2D(_MainTex, tc + float2( o.x,    0));
                col += tex2D(_MainTex, tc + float2(-o.x,  o.y));
                col += tex2D(_MainTex, tc + float2(   0,  o.y));
                col += tex2D(_MainTex, tc + float2( o.x,  o.y));
                return col / 9.0;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 tc  = ScaleUV(IN.texcoord);
                half4  col = SampleBlurred(tc) * IN.color;

                // Premultiplied alpha — required for Blend One OneMinusSrcAlpha
                col.rgb *= col.a;
                return fixed4(col);
            }
            ENDCG
        }
    }
}
