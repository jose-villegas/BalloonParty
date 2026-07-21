Shader "BalloonParty/Display/PaintingFieldDecay"
{
    // Per-tick decay blit for the painting field: subtracts _DecayRate * _DeltaTime from the
    // alpha channel each tick. When alpha drops below epsilon, clears RGB to black so dead
    // texels don't carry stale color into future blends.
    Properties
    {
        [HideInInspector] _MainTex ("Source (read RT)", 2D) = "black" {}
        _DecayRate  ("Decay Rate",  Float) = 0.15
        _DeltaTime  ("Delta Time",  Float) = 0.05
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
            #pragma target 3.0

            sampler2D _MainTex;
            float _DecayRate;
            float _DeltaTime;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 data = tex2D(_MainTex, i.uv);
                float3 color = data.rgb;
                float alpha = data.a;

                // Linear decay.
                alpha = max(0.0, alpha - _DecayRate * _DeltaTime);

                // Clear color when invisible (avoids stale colors in future blends).
                color *= step(0.001, alpha);

                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
