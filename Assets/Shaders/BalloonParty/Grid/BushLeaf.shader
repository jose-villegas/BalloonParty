Shader "BalloonParty/Grid/BushLeaf"
{
    Properties
    {
        _MainTex ("Leaf Atlas", 2D) = "white" {}

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)           = (0.15, 0.18, 0.1, 0.55)
        _ShadowOffset   ("Offset",   Vector)          = (0.1, -0.1, 0, 0)
        _ShadowSoftness ("Softness", Range(0, 0.08))  = 0.015

        [Header(Highlight)]
        _HighlightColor    ("Color",     Color)          = (1, 1, 0.9, 0.4)
        _HighlightOffset   ("Offset",    Vector)         = (-0.06, 0.08, 0, 0)
        _HighlightSize     ("Size",      Range(0.01, 0.3)) = 0.1
        _HighlightSoftness ("Softness",  Range(0.01, 0.3)) = 0.12

        [Header(Sprite)]
        _SpriteScale ("Scale", Range(0.3, 1.0)) = 0.75
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
            fixed4 _HighlightColor;
            float2 _HighlightOffset;
            float  _HighlightSize;
            float  _HighlightSoftness;
            float  _SpriteScale;

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _LeafTint)
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
                float2 localShadowOffset : TEXCOORD3;
                float2 localHighlightOffset : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);

                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props, _UVRect);
                o.rawUV = v.uv;
                o.uvRect = rect;

                // Scale sprite inward from center to create margins for shadow
                // Divides UV outward: sprite occupies the center, margins are transparent
                float2 spriteUV = (v.uv - 0.5) / _SpriteScale + 0.5;
                o.uv = rect.xy + spriteUV * rect.zw;

                // Inverse-rotate shadow offset from world space into local UV space
                // Use UNITY_MATRIX_M for correct per-instance matrix on all platforms
                float cosA = UNITY_MATRIX_M[0].x;
                float sinA = UNITY_MATRIX_M[1].x;
                float len = sqrt(cosA * cosA + sinA * sinA);
                cosA /= len;
                sinA /= len;
                // R(-θ) = [[cos, sin], [-sin, cos]], negated to correct direction
                o.localShadowOffset = -float2(
                     cosA * _ShadowOffset.x + sinA * _ShadowOffset.y,
                    -sinA * _ShadowOffset.x + cosA * _ShadowOffset.y);

                o.localHighlightOffset = -float2(
                     cosA * _HighlightOffset.x + sinA * _HighlightOffset.y,
                    -sinA * _HighlightOffset.x + cosA * _HighlightOffset.y);

                return o;
            }

            inline float2 RemapUV(float2 rawUV, float4 rect)
            {
                return rect.xy + rawUV * rect.zw;
            }

            inline fixed SampleShadowAlpha(float2 rawUV, float4 rect)
            {
                // Scale same as sprite so shadow silhouette matches leaf shape
                float2 scaled = (rawUV - 0.5) / _SpriteScale + 0.5;
                float2 uv = RemapUV(scaled, rect);
                // Mask out samples past the quad edge to prevent wrap-around
                float2 inside = step(0.0, rawUV) * step(rawUV, 1.0);
                return tex2D(_MainTex, uv).a * inside.x * inside.y;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Bounds check: outside [0,1] after scaling is beyond the sprite
                float2 spriteUV = (i.rawUV - 0.5) / _SpriteScale + 0.5;
                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float spriteMask = inBounds.x * inBounds.y;

                float2 shadowRaw = i.rawUV + i.localShadowOffset;
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
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _LeafTint);
                col *= tint;
                col.a *= spriteMask;

                // Specular highlight: radial falloff from offset center, masked to leaf shape
                float2 hlCenter = spriteUV - 0.5 + i.localHighlightOffset / _SpriteScale;
                float hlDist = length(hlCenter);
                float hlFalloff = 1.0 - smoothstep(_HighlightSize, _HighlightSize + _HighlightSoftness, hlDist);
                float hlMask = hlFalloff * col.a;
                col.rgb = lerp(col.rgb, _HighlightColor.rgb, hlMask * _HighlightColor.a);

                // Composite: shadow behind, leaf on top (Porter-Duff "over")
                fixed3 rgb = col.rgb * col.a + shadow.rgb * shadow.a * (1.0 - col.a);
                fixed  a   = col.a + shadow.a * (1.0 - col.a);
                return fixed4(rgb / max(a, 0.001), a);
            }
            ENDCG
        }
    }
}

