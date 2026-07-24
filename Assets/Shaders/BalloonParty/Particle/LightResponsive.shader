Shader "BalloonParty/Particle/LightResponsive"
{
    // A particle material whose intensity follows the scene light's COLOUR magnitude: bright in
    // daylight, dim at night, tracking the day/night gradient for free. This is the cheap path — it
    // reads only the global _SceneLightColor (a uniform TimeOfDayService already pushes), no light-field
    // sample and no per-particle world position, so the cost is a constant-register fetch + a couple of
    // ALU ops per fragment on top of the usual particle fill. Blend mode is exposed so the same shader
    // serves additive glows (the default) and alpha-blended puffs.
    Properties
    {
        [PerRendererData] _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Scene Light Response)]
        // 0 = ignore the scene light (authored intensity); 1 = fully scale intensity by the normalized
        // light-colour magnitude, so the particle brightens in daylight and dims at night.
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
        // The light-colour magnitude that counts as full intensity (~1.73 = white daylight at intensity
        // 1). The magnitude is normalized against this, so daylight leaves the authored intensity intact.
        _LightFullAt ("Full-Light Level", Range(0.1, 3)) = 1.73

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1 // One (additive)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _LightInfluence;
            float _LightFullAt;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                fixed4 c = tex2D(_MainTex, IN.uv) * IN.color;

                // Scale intensity by the normalized magnitude of the scene-light colour. SceneLightTint()
                // is the flat global (no field sample); its fallback is 1 before the owner pushes, so a
                // magnitude of _LightFullAt (daylight) leaves the authored intensity intact and a dim
                // night colour fades it. Influence 0 opts out entirely.
                float lit = saturate(length(SceneLightTint()) / _LightFullAt);
                c.rgb *= lerp(1.0, lit, _LightInfluence);

                return c;
            }
            ENDCG
        }
    }
}
