Shader "BalloonParty/Grid/BushBranch"
{
    Properties
    {
        _MainTex ("Branch Map", 2D) = "white" {}
        _BranchColor ("Branch Color", Color) = (0.35, 0.22, 0.10, 1)
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)            = (0.06, 0.06, 0.14, 0.35)
        _ShadowOffset   ("Offset",   Vector)           = (0.04, -0.06, 0, 0)
        _ShadowSpread   ("Spread",   Range(0, 1))      = 0.15
        _ShadowSoftness ("Softness", Range(0, 0.08))   = 0.02

        [Header(Sprite)]
        _SpriteScale ("Scale", Range(0.3, 1.0)) = 0.85
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
            fixed4 _BranchColor;
            float _AlphaCutoff;
            fixed4 _ShadowColor;
            float2 _ShadowOffset;
            float  _ShadowSpread;
            float  _ShadowSoftness;
            float  _SpriteScale;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

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
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.rawUV = v.uv;
                o.uv = (v.uv - 0.5) / _SpriteScale + 0.5;
                return o;
            }

            // Radial ground shadow projected from bush centre (0.5, 0.5).
            // _ShadowOffset — directional bias: how the shadow reprojects
            //   from the centre (light direction knob).
            // _ShadowSpread — widens the shadow silhouette by scaling the
            //   lookup UV toward the centre. At spread=0 shadow matches
            //   source 1:1; at spread>0 shadow is magnified like a
            //   point-light projection from above the trunk.
            // Shadow alpha is purely _ShadowColor.a — no depth blending.
            inline fixed SampleShadow(float2 scaledUV, float2 blurOfs)
            {
                float2 sampleUV = scaledUV + blurOfs;

                // Shift by directional offset (reproject from centre)
                float2 sourceUV = sampleUV - _ShadowOffset;

                // Widen: scale toward centre so shadow is larger than source
                float scale = 1.0 / (1.0 + _ShadowSpread);
                sourceUV = 0.5 + (sourceUV - 0.5) * scale;

                float2 inside = step(0.0, sourceUV) * step(sourceUV, 1.0);
                fixed a = tex2D(_MainTex, sourceUV).a;
                return step(_AlphaCutoff, a) * inside.x * inside.y;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Bounds check for sprite content
                float2 inBounds = step(0.0, i.uv) * step(i.uv, 1.0);
                float spriteMask = inBounds.x * inBounds.y;

                // Shadow — 9-tap blur
                float s = _ShadowSoftness;

                fixed shadowHit;
                if (s > 0.001)
                {
                    shadowHit = (
                        SampleShadow(i.uv, float2(-s, -s)) +
                        SampleShadow(i.uv, float2( 0, -s)) +
                        SampleShadow(i.uv, float2( s, -s)) +
                        SampleShadow(i.uv, float2(-s,  0)) +
                        SampleShadow(i.uv, float2( 0,  0)) +
                        SampleShadow(i.uv, float2( s,  0)) +
                        SampleShadow(i.uv, float2(-s,  s)) +
                        SampleShadow(i.uv, float2( 0,  s)) +
                        SampleShadow(i.uv, float2( s,  s))
                    ) / 9.0;
                }
                else
                {
                    shadowHit = SampleShadow(i.uv, float2(0, 0));
                }

                fixed4 shadow = fixed4(_ShadowColor.rgb, _ShadowColor.a * shadowHit);

                // Branch content
                fixed4 map = tex2D(_MainTex, i.uv);
                float branchAlpha = step(_AlphaCutoff, map.a) * spriteMask;
                fixed3 col = _BranchColor.rgb * (0.6 + 0.4 * map.a) * _RendererColor.rgb;

                // Composite: shadow behind branch (Porter-Duff "over")
                fixed3 rgb = col * branchAlpha + shadow.rgb * shadow.a * (1.0 - branchAlpha);
                fixed  a   = branchAlpha + shadow.a * (1.0 - branchAlpha);
                return fixed4(rgb / max(a, 0.001), a);
            }
            ENDCG
        }
    }
}

