Shader "Hidden/BalloonParty/Display/EffectsClearingMask"
{
    // One frame of the effects clearing mask (EffectsClearingMaskView, PLAN-EffectsLayerClearing): last frame's
    // mask, decayed, then each ball in flight stamped on it as a capsule from where it was to where it is. The
    // stamp is 1 - (d/r)^2, highest on the path, so a uniform decay shrinks the cleared area inward over time.
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

            sampler2D _MainTex;
            float  _Decay;
            float4 _Stamps[MAX_STAMPS];
            float  _StampCount;
            float  _Radius;
            float  _Aspect;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).r - _Decay;

                // Distances in units of the view's height, so a stamp is round on any aspect.
                float2 scale = float2(_Aspect, 1.0);
                float2 p = i.uv * scale;

                [unroll]
                for (int s = 0; s < MAX_STAMPS; s++)
                {
                    float2 a = _Stamps[s].xy * scale;
                    float2 ab = _Stamps[s].zw * scale - a;
                    float t = saturate(dot(p - a, ab) / max(dot(ab, ab), 1e-8));
                    float d = length(p - a - ab * t) / _Radius;
                    float stamp = saturate(1.0 - d * d);
                    mask = max(mask, s < _StampCount ? stamp : 0.0);
                }

                return saturate(mask).xxxx;
            }
            ENDCG
        }
    }
}
