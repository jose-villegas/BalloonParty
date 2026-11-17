Shader "Hidden/BalloonParty/Display/EffectsClearingMask"
{
    // One frame of the effects clearing mask (EffectsClearingMaskView, PLAN-EffectsLayerClearing): last frame's
    // mask, decayed, then each ball in flight stamped on it as a capsule from where it was to where it is. The
    // stamp is 1 - (d/r)^2, highest on the path, so a uniform decay shrinks the cleared area inward over time.
    // Red is that value; green is the radius of the stamp that set it (in view heights), which the composite's
    // glass needs to know how big the dome under each point is. Each ball's radius follows its shields.
    // Last frame's mask is read through the camera's move and zoom since (_Reproject), so a stamp stays on the
    // world when the camera moves (the level-up cinematic zooms in on the ball), and its radius in view heights
    // grows with the zoom (_RadiusScale). A stamp's fade (_Fades, 0 to 1) takes that much off its dome, as the
    // decay would have by then: a ball gone from flight shrinks inward to nothing even when the mask is wiped
    // each frame (no trail).
    Properties
    {
        _MainTex ("Previous Mask", 2D) = "black" {}
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

            #define MAX_STAMPS 8

            sampler2D_half _MainTex;
            float  _Decay;
            float4 _Stamps[MAX_STAMPS];
            float  _Radii[MAX_STAMPS];
            float  _Fades[MAX_STAMPS];
            float  _StampCount;
            float  _Aspect;
            float4 _Reproject;
            float  _RadiusScale;

            float4 frag(v2f_img i) : SV_Target
            {
                float2 from = i.uv * _Reproject.xy + _Reproject.zw;
                float2 previous = all(from == saturate(from)) ? tex2D(_MainTex, from).rg : float2(0.0, 0.0);
                float mask = previous.r - _Decay;
                float radius = previous.g * _RadiusScale;

                // Distances in units of the view's height, so a stamp is round on any aspect.
                float2 scale = float2(_Aspect, 1.0);
                float2 p = i.uv * scale;

                [unroll]
                for (int s = 0; s < MAX_STAMPS; s++)
                {
                    float2 a = _Stamps[s].xy * scale;
                    float2 ab = _Stamps[s].zw * scale - a;
                    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-8));
                    float d = length(p - a - ab * t) / max(_Radii[s], 1e-4);
                    float stamp = s < _StampCount ? saturate(1.0 - d * d - _Fades[s]) : 0.0;
                    if (stamp > mask)
                    {
                        mask = stamp;
                        radius = _Radii[s];
                    }
                }

                return float4(saturate(mask), radius, 0.0, 0.0);
            }
            ENDCG
        }
    }
}
