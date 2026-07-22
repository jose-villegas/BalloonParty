Shader "BalloonParty/Display/PaintingFieldDecay"
{
    // Per-tick decay blit for the painting field: subtracts _DecayRate * _DeltaTime from the
    // alpha channel each tick. Edge texels (those with low-alpha neighbors) decay faster via
    // _ErosionRate, causing paint stamps to shrink inward over time. When alpha drops below
    // epsilon, clears RGB to black so dead texels don't carry stale color into future blends.
    Properties
    {
        [HideInInspector] _MainTex ("Source (read RT)", 2D) = "black" {}
        _DecayRate    ("Decay Rate",    Float) = 0.08
        _ErosionRate  ("Erosion Rate",  Float) = 0.12
        _DeltaTime    ("Delta Time",    Float) = 0.05
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
            float4 _MainTex_TexelSize;
            float _DecayRate;
            float _ErosionRate;
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

                // Edge erosion: sample 4 neighbors to detect boundary texels.
                float2 tx = _MainTex_TexelSize.xy;
                float nAlpha = tex2D(_MainTex, i.uv + float2( tx.x, 0)).a
                             + tex2D(_MainTex, i.uv + float2(-tx.x, 0)).a
                             + tex2D(_MainTex, i.uv + float2(0,  tx.y)).a
                             + tex2D(_MainTex, i.uv + float2(0, -tx.y)).a;
                float avgNeighbor = nAlpha * 0.25;

                // Erosion factor: 1 at edges (low neighbor avg), 0 at solid interior.
                float edgeness = 1.0 - saturate(avgNeighbor / max(alpha, 0.001));
                float erosion = edgeness * _ErosionRate * _DeltaTime;

                // Linear decay + edge erosion.
                alpha = max(0.0, alpha - _DecayRate * _DeltaTime - erosion);

                // Clear color when invisible (avoids stale colors in future blends).
                color *= step(0.001, alpha);

                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
