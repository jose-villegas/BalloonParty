Shader "BalloonParty/Particle/Glow"
{
    // An unlit sprite, line or particle whose colour can reach past white into the HDR range, for the few
    // effects meant to glow (the lightning, the laser beam). Vertex and sprite colours are 8-bit, so a
    // renderer's own colour stops at 1; _HdrBoost multiplies after it, on the material. The tonemapper rolls
    // the result off toward white and bloom spreads whatever passes its threshold into a halo. At _HdrBoost 1
    // this is the built-in Mobile/Particles shaders' look (texture × vertex colour), with the blend exposed
    // so one shader serves the alpha-blended (the default) and the additive materials.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        // 1 = the authored colour. Above 1 the effect is brighter than white; keep it to the few that
        // should bloom, and below about 4 (gamma units, so about 20x in linear light).
        _HdrBoost ("HDR Boost", Range(0, 8)) = 1
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
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
        // Alpha always blends as coverage, as a sprite's does: the scene capture reads it as an occluder.
        Blend    [_SrcBlend] [_DstBlend], One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                half4 color   : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // half, not fixed: OpenGL ES may run fixed at lowp, which only reaches 2.
                half4 color   : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(half4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                half4 _RendererColor;
            #endif

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _HdrBoost;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color * _RendererColor;
                OUT.color.rgb *= _HdrBoost;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                return tex2D(_MainTex, IN.uv) * IN.color;
            }
            ENDCG
        }
    }
}
