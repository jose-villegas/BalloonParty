Shader "BalloonParty/Particle/SpecularDrop"
{
    // Shades a flat circle particle as a glossy sphere/drop — a billboard sphere impostor: the fragment
    // reconstructs a hemisphere normal from the quad UV and lights it with the scene light direction
    // (_SceneLightDir), so the drops catch a moving highlight consistent with the rest of the scene. No
    // mesh, no normal-map texture, no field sample — a handful of ALU + one sqrt + one pow per fragment
    // on top of the usual particle fill. The silhouette comes from the sprite's own alpha, so a round
    // circle reads as a ball and a teardrop sprite as a rounded drop. Ambient is floored high on purpose
    // so the paint COLOUR (gameplay info) stays identifiable — the shading is a sheen, not a dark ball.
    Properties
    {
        [PerRendererData] _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Sphere Shading)]
        // Opacity floor of the diffuse term — how lit the shadow side stays. High (~0.6) keeps the paint
        // colour readable; 1 = flat (no shading).
        _Ambient ("Ambient Floor", Range(0, 1)) = 0.6
        // Out-of-plane tilt of the light for shading. The scene light is 2D (in-plane); this lifts it
        // toward the viewer so the highlight sits on the drop's face instead of grazing the rim.
        _LightDepth ("Light Depth", Range(0.05, 2)) = 0.5

        [Header(Specular)]
        _Gloss ("Gloss (tightness)", Range(1, 128)) = 24
        _SpecStrength ("Specular Strength", Range(0, 2)) = 0.6

        [Header(Scene Light Response)]
        // 0 = intensity independent of the scene light (the direction-driven shading above is always
        // on); 1 = the whole drop dims/brightens with the normalized light-colour magnitude, so it
        // belongs to the day/night (full at noon, dim at night). Opt-in, free global read.
        _LightInfluence ("Light Influence", Range(0, 1)) = 0
        // The light-colour magnitude that counts as full intensity (~1.73 = white daylight at
        // intensity 1). Only used when Light Influence > 0.
        _LightFullAt ("Full-Light Level", Range(0.1, 3)) = 1.73

        [Header(Blend)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10 // OneMinusSrcAlpha
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
            fixed4 _Color;
            float _Ambient;
            float _LightDepth;
            float _Gloss;
            float _SpecStrength;
            float _LightInfluence;
            float _LightFullAt;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.uv;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                fixed4 albedo = tex2D(_MainTex, IN.uv) * IN.color;

                // Sphere impostor: UV → hemisphere normal. z = sqrt(1 - r²) is saturated so the quad
                // corners (r² > 1) don't NaN — they're transparent in the sprite alpha anyway. (p, z) is
                // already unit length, so no normalize needed.
                float2 p = IN.uv * 2.0 - 1.0;
                float z = sqrt(saturate(1.0 - dot(p, p)));
                float3 normal = float3(p, z);

                // Light the impostor with the scene light, tilted out of plane so the highlight lands on
                // the face. View direction is +z (camera looks down -z at the billboard).
                float3 lightDir = normalize(float3(SceneLightDirection(), _LightDepth));
                float diffuse = lerp(_Ambient, 1.0, saturate(dot(normal, lightDir)));

                float3 halfDir = normalize(lightDir + float3(0.0, 0.0, 1.0));
                float specular = pow(saturate(dot(normal, halfDir)), _Gloss) * _SpecStrength;

                fixed4 c;
                c.rgb = albedo.rgb * diffuse + specular;

                // Optional: scale the whole drop by the normalized scene-light magnitude so it dims into
                // night with everything else. SceneLightTint() is the flat global (no field sample); its
                // fallback is 1 before the owner pushes, so influence 0 leaves the drop fully lit.
                float lit = saturate(length(SceneLightTint()) / _LightFullAt);
                c.rgb *= lerp(1.0, lit, _LightInfluence);

                c.a = albedo.a;
                return c;
            }
            ENDCG
        }
    }
}
