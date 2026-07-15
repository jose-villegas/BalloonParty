Shader "BalloonParty/Grid/BushBranch"
{
    Properties
    {
        _MainTex ("Branch Map", 2D) = "white" {}
        _BranchGradient ("Branch Gradient", 2D) = "white" {}
        _BranchColor ("Branch Color", Color) = (0.35, 0.22, 0.10, 1)

        // Diffuse response to the scene light (colour x intensity, like Sprite/Diffuse):
        // 0 = unlit (authored look always), 1 = fully lit.
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.01
        _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)            = (0.06, 0.06, 0.14, 0.35)
        // Direction now derives from the scene light (see SceneLightDirection below); this is
        // just the distance along that direction. Default reproduces the BushSettings-authored
        // (0.04, -0.05) offset (|.| = 0.0640).
        _ShadowDistance ("Distance", Range(0, 0.3))    = 0.0640
        _ShadowSpread   ("Spread",   Range(0, 1))      = 0.15
        _ShadowSoftness ("Softness", Range(0, 0.08))   = 0.02

        [Header(AO)]
        _AOColor     ("Color",     Color)           = (0.02, 0.02, 0.06, 0.4)
        _AORadius    ("Radius",    Range(0.05, 1))  = 0.45
        _AOSoftness  ("Softness",  Range(0.01, 1))  = 0.3
        _AOIntensity ("Intensity", Range(0, 2))     = 1

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
            #include "../Include/SceneLight.cginc"
            #include "../Include/SpriteScale.cginc"
            #include "../Include/Composite.cginc"

            sampler2D _MainTex;
            sampler2D _BranchGradient;
            fixed4 _BranchColor;
            float _AlphaCutoff;
            fixed4 _ShadowColor;
            float  _ShadowDistance;
            float  _ShadowSpread;
            float  _ShadowSoftness;
            fixed4 _AOColor;
            float  _AORadius;
            float  _AOSoftness;
            float  _AOIntensity;
            float  _SpriteScale;
            float  _LightInfluence;

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
                float2 worldPos : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.rawUV = v.uv;
                o.uv = ScaleSpriteUV(v.uv, _SpriteScale);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xy;
                return o;
            }

            // Radial ground shadow projected from bush centre (0.5, 0.5).
            // Direction reprojects away from the scene light; only _ShadowDistance stays
            //   authored, so rotating the light moves every branch's shadow together.
            // _ShadowSpread — widens the shadow silhouette by scaling the
            //   lookup UV toward the centre. At spread=0 shadow matches
            //   source 1:1; at spread>0 shadow is magnified like a
            //   point-light projection from above the trunk.
            // Shadow alpha is purely _ShadowColor.a — no depth blending.
            inline fixed SampleShadow(float2 scaledUV, float2 blurOfs, float2 worldPos)
            {
                float2 sampleUV = scaledUV + blurOfs;

                // Shift by directional offset (reproject from centre)
                float2 shadowOffset = -SceneLightDirectionAt(worldPos) * _ShadowDistance;
                float2 sourceUV = sampleUV - shadowOffset;

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
                float spriteMask = SpriteBoundsMask(i.uv);

                // Shadow — 9-tap blur
                float s = _ShadowSoftness;

                fixed shadowHit;
                if (s > 0.001)
                {
                    shadowHit = (
                        SampleShadow(i.uv, float2(-s, -s), i.worldPos) +
                        SampleShadow(i.uv, float2( 0, -s), i.worldPos) +
                        SampleShadow(i.uv, float2( s, -s), i.worldPos) +
                        SampleShadow(i.uv, float2(-s,  0), i.worldPos) +
                        SampleShadow(i.uv, float2( 0,  0), i.worldPos) +
                        SampleShadow(i.uv, float2( s,  0), i.worldPos) +
                        SampleShadow(i.uv, float2(-s,  s), i.worldPos) +
                        SampleShadow(i.uv, float2( 0,  s), i.worldPos) +
                        SampleShadow(i.uv, float2( s,  s), i.worldPos)
                    ) / 9.0;
                }
                else
                {
                    shadowHit = SampleShadow(i.uv, float2(0, 0), i.worldPos);
                }

                // No light, no shadow — opacity follows intensity (clamped at authored).
                fixed4 shadow = fixed4(_ShadowColor.rgb, _ShadowColor.a * shadowHit * ShadowLightFadeAt(i.worldPos));

                // AO blob — radial gradient centred at trunk, darkens ground
                float dist = length(i.uv - 0.5) * 2.0;
                fixed aoAlpha = _AOColor.a * _AOIntensity * (1.0 - smoothstep(
                    _AORadius - _AOSoftness, _AORadius, dist));
                fixed4 ao = fixed4(_AOColor.rgb, aoAlpha);

                // Branch content — gradient mapped across width (B channel)
                fixed4 map = tex2D(_MainTex, i.uv);
                float branchAlpha = step(_AlphaCutoff, map.a) * spriteMask;
                fixed3 gradCol = tex2D(_BranchGradient, float2(map.b, 0.5)).rgb;
                fixed3 col = gradCol * _BranchColor.rgb * (0.6 + 0.4 * map.a) * _RendererColor.rgb;

                // Diffuse term: the branch body is lit by the scene light — same response as
                // Sprite/Diffuse. The AO blob stays authored (ambient occlusion, not light-cast).
                col *= lerp(float3(1.0, 1.0, 1.0), SceneLightTintAt(i.worldPos), _LightInfluence);

                // Composite: AO (bottom) ← shadow ← branch (top)
                fixed4 base = PorterDuffOver(shadow, ao);
                fixed4 branch = fixed4(col, branchAlpha);
                return PorterDuffOver(branch, base);
            }
            ENDCG
        }
    }
}

