Shader "Hidden/BalloonParty/SceneLightFieldFill"
{
    // Fills the whole scene-light field with the directional system's REST state in one blit:
    // R = the global magnitude, GB = the 0.5-biased toward-light direction, A = 0 (no palette
    // colour — consumers use _SceneLightColor). No stamps yet; the palette encode lands in Phase C.
    // The source texture is ignored — the fragment writes a constant computed from the properties.
    Properties
    {
        _MainTex        ("Source (ignored)", 2D)    = "black" {}
        _FillMagnitude  ("Magnitude (R)",    Float) = 1.0
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

            float  _FillMagnitude;
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

            // float4, not fixed4 — magnitude can reach 2 (the owner's intensity range) and the
            // target is a half-float RT; fixed precision would quantize it on mobile compilers.
            float4 frag(v2f i) : SV_Target
            {
                // GB = dir * 0.5 + 0.5; A = 0 exactly (no palette colour anywhere at rest).
                float2 gb = _FillDir.xy * 0.5 + 0.5;
                return float4(_FillMagnitude, gb.x, gb.y, 0.0);
            }
            ENDCG
        }
    }
}
