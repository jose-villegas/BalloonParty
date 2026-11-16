Shader "Hidden/BalloonParty/Display/EffectsClearingMask"
{
    // One frame of the effects clearing mask (EffectsClearingMaskView, PLAN-EffectsLayerClearing): last frame's
    // mask, decayed, then each ball in flight stamped on it as a capsule from where it was to where it is. The
    // stamp is 1 - (d/r)^2, highest on the path, so a uniform decay shrinks the cleared area inward over time.
    // Red is that value; green is the radius of the stamp that set it (in view heights), which the composite's
    // glass needs to know how big the dome under each point is. Each ball's radius follows its shields.
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
            float  _StampCount;
            float  _Aspect;

            float4 frag(v2f_img i) : SV_Target
            {
                float2 previous = tex2D(_MainTex, i.uv).rg;
                float mask = previous.r - _Decay;
                float radius = previous.g;

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
                    float stamp = s < _StampCount ? saturate(1.0 - d * d) : 0.0;
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
