Shader "Hidden/BalloonParty/Scenario/WaterCaustics"
{
    // The light the water surface focuses on the bottom (WaterCausticBuffer): the same surface Water.shader
    // draws (WaterSurface.cginc), turned into brightness where it curves like a converging lens. Drawn into a
    // small target over the camera's rect, so sampling it back is already soft; Water.shader reads it under the
    // surface, slightly enlarged, wobbled and shifted away from the sun.
    //
    // Out: r = focused light (0 = none, 1 = a bright line). The target spans the disturbance field's rect.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, linear)", 2D) = "gray" {}
        _WaveSize     ("Wave Size (world)",      Float) = 2.2
        _WaveSpeed    ("Wave Drift (world/s)",   Float) = 0.12
        _WaveHeight   ("Wave Height",            Float) = 0.06
        _RippleRings  ("Rings Per Push",         Float) = 5
        _RippleSpeed  ("Ring Speed (radians/s)", Float) = 7
        _RippleHeight ("Ring Height",            Float) = 0.05
        // How much light a curve focuses: more = brighter, thinner lines.
        _CausticFocus ("Focus",                  Float) = 1.5
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/WaterSurface.cginc"

            float _CausticFocus;

            float4 frag(v2f_img IN) : SV_Target
            {
                float2 wp = _FieldBoundsMin + IN.uv * _FieldBoundsSize;

                // A surface that bulges up (negative curvature) gathers the light under it into a line, one that
                // dips spreads it thin: the classic caustic net.
                float s = WATER_SLOPE_STEP;
                float h = WaterHeight(wp);
                float curvature = (WaterHeight(wp + float2(s, 0.0)) + WaterHeight(wp - float2(s, 0.0))
                                 + WaterHeight(wp + float2(0.0, s)) + WaterHeight(wp - float2(0.0, s)) - 4.0 * h)
                                 / (s * s);
                float light = saturate(-curvature * _CausticFocus * 0.1);
                return float4(light * light, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
}
