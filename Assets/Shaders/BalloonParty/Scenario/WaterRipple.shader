Shader "Hidden/BalloonParty/Scenario/WaterRipple"
{
    // One step of the water's ripples (WaterRippleBuffer): the wave equation over a height field the size of the
    // disturbance field, in its UV space. Wherever the field gained a push since the last step (a ball arriving, a
    // pop) the water is pressed down; the dip then spreads outward as rings at Ring Speed, rings cross and pass
    // through each other, and they fade over Fade seconds. The field's own push only starts a ripple: the water
    // never holds the field's shape, so nothing wobbles in place.
    //
    // In: _MainTex = the state one step ago, _DisturbanceTex = the field now, _PreviousField = the field at the
    // last step that took its gain. Out: r = height now, g = height one step ago (RGHalf, signed).
    Properties
    {
        _MainTex      ("Previous State",          2D)    = "black" {}
        // How fast a ring walks outward. Capped where one step would cross more than two thirds of a texel, past
        // which the simulation blows up.
        _RippleSpeed  ("Ring Speed (world/s)",    Float) = 2.4
        // Seconds for a ring to lose most of its height (the time constant).
        _RippleFade   ("Fade (seconds)",          Float) = 1.4
        // How deep a fresh push presses the water.
        _RippleDrive  ("Drive",                   Float) = 1.2
        // Texels at the field's edges that soak rings up, so they leave the screen rather than bounce back in.
        _RippleSponge ("Edge Sponge (texels)",    Float) = 6
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            sampler2D _DisturbanceTex;
            sampler2D _PreviousField;
            float     _RippleSpeed;
            float     _RippleFade;
            float     _RippleDrive;
            float     _RippleSponge;

            // Set by WaterRippleBuffer: the fixed step, the state's resolution, and 1 on the step that takes the
            // field's gain since the last one (0 on the frame's further steps).
            float _StepSeconds;
            float _TexelsPerUnit;
            float _TakeDrive;

            // How hard the field pushes at a texel: displacement length plus density, about 1 under a fresh stamp.
            float PushOf(float3 field)
            {
                return length(field.gb - 0.5) * 2.0 + abs(field.r - 0.5) * 2.0;
            }

            float4 frag(v2f_img IN) : SV_Target
            {
                float2 uv = IN.uv;
                float2 texel = _MainTex_TexelSize.xy;
                float2 state = tex2D(_MainTex, uv).rg;
                float around = tex2D(_MainTex, uv + float2(texel.x, 0.0)).r
                             + tex2D(_MainTex, uv - float2(texel.x, 0.0)).r
                             + tex2D(_MainTex, uv + float2(0.0, texel.y)).r
                             + tex2D(_MainTex, uv - float2(0.0, texel.y)).r;

                // Texels a ring crosses per step, squared: past 0.5 the explicit step is unstable.
                float courant = _RippleSpeed * _StepSeconds * _TexelsPerUnit;
                float courant2 = min(courant * courant, 0.45);
                float height = state.r;
                float next = 2.0 * height - state.g + courant2 * (around - 4.0 * height);
                next *= exp(-_StepSeconds / max(_RippleFade, 0.05));

                // Only a push that grew presses the water: the field easing back off, or holding, starts nothing. A
                // pop's big gain presses only a little deeper than a passing ball's, so pops don't drown the rest.
                float grew = max(0.0, PushOf(tex2D(_DisturbanceTex, uv).rgb) - PushOf(tex2D(_PreviousField, uv).rgb));
                next -= _RippleDrive * grew / (1.0 + 2.0 * grew) * _TakeDrive;

                float2 edge = min(uv, 1.0 - uv) / max(texel * _RippleSponge, 1e-5);
                next *= lerp(0.8, 1.0, saturate(min(edge.x, edge.y)));

                return float4(clamp(next, -4.0, 4.0), height, 0.0, 0.0);
            }
            ENDCG
        }
    }
}
