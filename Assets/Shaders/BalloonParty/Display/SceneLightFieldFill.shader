Shader "Hidden/BalloonParty/SceneLightFieldFill"
{
    // Clears the whole scene-light field to its rest state — purely LOCAL, ambient-independent:
    //   R = 0   — no local boost (the ambient magnitude is the global _SceneLightIntensity, added by
    //             the consumers).
    //   GB = 0.5 — neutral / zero direction (the consumers read the ambient direction from the global
    //             _SceneLightDir and blend the local direction in by how much local light is here).
    //   A = 0   — no palette tag.
    // A constant, so the field carries nothing about the ambient light — only what local lights stamp.
    // Source texture ignored.
    Properties
    {
        _MainTex ("Source (ignored)", 2D) = "black" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            // float4, not fixed4 — the accumulate pass adds boosts up to the ceiling and the target is a
            // half-float RT; fixed precision would quantize it on mobile compilers.
            float4 frag(v2f i) : SV_Target
            {
                return float4(0.0, 0.5, 0.5, 0.0);
            }
            ENDCG
        }
    }
}
