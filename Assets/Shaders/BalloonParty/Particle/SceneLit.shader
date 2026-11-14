Shader "BalloonParty/Particle/SceneLit"
{
    // An alpha-blended particle lit by the scene light like the balloon it came from, for the plates a hard
    // balloon sheds when it breaks. The sheet is drawn in neutral white light (Tools/art/shatter), so the
    // time of day here is the only tint on it: without it the shards would keep their daylight colour at
    // night, beside a board gone blue. Texture × vertex colour × _Color, as the built-in
    // Mobile/Particles/Alpha Blended, then × the light at the shard (local lights included), re-graded with
    // the same knobs as its balloon's material, and capped at white so a hot local light never blooms it.
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Scene Light)]
        // 0 = unlit (the authored colour), 1 = fully lit by the scene light. Match the balloon's material.
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
        // How much of the time of day's hue and darkening the shard keeps (1, 1 = all of it), as the
        // balloon shaders' Ambient Hue / Ambient Dimming. Match the balloon's material.
        _AmbientHue ("Ambient Hue", Range(0, 1)) = 1
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 1
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
        // Alpha blends as coverage, as a sprite's does: the scene capture reads it as an occluder.
        Blend    SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha

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
                half4 color   : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                half4 color   : COLOR;
                float2 uv     : TEXCOORD0;
                // half, not fixed: OpenGL ES may run fixed at lowp, which only reaches 2.
                half3 lightTint : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            float _LightInfluence;
            float _AmbientHue;
            float _AmbientDimming;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                // Sampled per vertex: a shard is small next to the field's texels, so its corners agree.
                float2 worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                float3 tint = SceneLightTintAtLOD(worldPos) + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);
                OUT.lightTint = lerp(float3(1.0, 1.0, 1.0), tint, _LightInfluence);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                half4 c = tex2D(_MainTex, IN.uv) * IN.color;
                c.rgb = SceneLightCapToWhite(c.rgb * IN.lightTint);
                return c;
            }
            ENDCG
        }
    }
}
