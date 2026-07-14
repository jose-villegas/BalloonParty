Shader "Hidden/BalloonParty/SceneLightFieldFill"
{
    // Fills the whole scene-light field with the rest state in one blit: R = 0 (the field carries only
    // the LOCAL light boost above the ambient — consumers add the ambient magnitude from the global
    // _SceneLightIntensity), GB = the 0.5-biased toward-light direction (the rest/fallback the gradient
    // pass keeps where there's no local light), A = 0 (no palette tag). Source texture ignored.
    Properties
    {
        _MainTex        ("Source (ignored)", 2D)    = "black" {}
        _FillDir        ("Direction (GB)",   Vector) = (-0.707, 0.707, 0, 0)
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

            float4 _FillDir; // xy = normalized toward-light direction

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
                // R = 0 (rest = no local light); GB = dir * 0.5 + 0.5; A = 0 (no palette tag).
                float2 gb = _FillDir.xy * 0.5 + 0.5;
                return float4(0.0, gb.x, gb.y, 0.0);
            }
            ENDCG
        }
    }
}
