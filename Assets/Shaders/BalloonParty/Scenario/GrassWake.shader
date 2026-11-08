Shader "Hidden/BalloonParty/Scenario/GrassWake"
{
    // One step of the grass wake (GrassWakeBuffer): what the blades bend to. The shared disturbance field
    // holds a push for seconds and spreads it, which suits the clouds' slow drift but reads as sluggish grass.
    // The wake takes only what the field GAINED since last frame (a ball arriving, a pop) and lets it go on its
    // own short recovery, so the blades snap down as the ball passes and spring back quickly behind it.
    //
    // In: _MainTex = last frame's wake, _DisturbanceTex = the field now, _PreviousField = the field last frame.
    // Out: rg = push (signed, at most unit length), b = stir (0..1). ARGBHalf, so no bias.
    Properties
    {
        _MainTex ("Previous Wake", 2D) = "black" {}
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
            sampler2D _DisturbanceTex;
            sampler2D _PreviousField;
            // How much of last frame's wake survives this frame: exp(-deltaTime / recovery seconds).
            float _WakeRetain;

            float4 frag(v2f_img IN) : SV_Target
            {
                float4 wake = tex2D(_MainTex, IN.uv);
                float3 now = tex2D(_DisturbanceTex, IN.uv).rgb;
                float3 before = tex2D(_PreviousField, IN.uv).rgb;

                // Field: r = density (0.5 rest), gb = displacement (0.5 rest).
                float2 pushNow = (now.gb - 0.5) * 2.0;
                float2 pushBefore = (before.gb - 0.5) * 2.0;

                // Only a push that grew this frame adds to the wake; the field easing back off adds nothing, so
                // the wake's own recovery decides how fast the blades stand up again.
                float grew = step(dot(pushBefore, pushBefore), dot(pushNow, pushNow));
                float2 push = wake.rg * _WakeRetain + (pushNow - pushBefore) * grew;
                push *= min(1.0, rsqrt(max(dot(push, push), 1e-6)));

                float stirNow = abs(now.r - 0.5) * 2.0;
                float stirBefore = abs(before.r - 0.5) * 2.0;
                float stir = saturate(wake.b * _WakeRetain + max(0.0, stirNow - stirBefore));

                return float4(push, stir, 0.0);
            }
            ENDCG
        }
    }
}
