Shader "BalloonParty/Display/PaintingFieldStamp"
{
    // Batched stamp blit for the painting field: writes RGB colors into the RT at stamp positions.
    // Up to 32 stamps per pass. Colors blend additively weighted by stamp mask — overlapping colors
    // mix naturally (blue + yellow → green-ish). RT layout: RGB = accumulated color, A = opacity.
    Properties
    {
        [HideInInspector] _MainTex ("Source (read RT)", 2D) = "black" {}
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

            #define MAX_STAMPS 32

            sampler2D _MainTex;
            int _StampCount;
            float4 _StampCenters[MAX_STAMPS];
            float _StampRadii[MAX_STAMPS];
            float4 _StampColors[MAX_STAMPS];

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
                float4 existing = tex2D(_MainTex, i.uv);
                float3 existingColor = existing.rgb;
                float existingAlpha = existing.a;

                float3 stampAccum = float3(0, 0, 0);
                float stampWeight = 0.0;

                for (int s = 0; s < _StampCount; s++)
                {
                    float2 b = _StampCenters[s].xy;
                    float2 a = _StampCenters[s].zw;
                    float radius = _StampRadii[s];
                    float3 stampColor = _StampColors[s].rgb;

                    // Capsule SDF: distance from pixel to the line segment a→b.
                    float2 pa = i.uv - a;
                    float2 ba = b - a;
                    float h = saturate(dot(pa, ba) / (dot(ba, ba) + 1e-6));
                    float dist = length(pa - ba * h);

                    float mask = 1.0 - smoothstep(radius * 0.7, radius, dist);
                    mask *= _StampColors[s].a;

                    stampAccum += stampColor * mask;
                    stampWeight += mask;
                }

                if (stampWeight < 0.001)
                {
                    return existing;
                }

                // Average color of all stamps hitting this texel.
                float3 newColor = stampAccum / stampWeight;

                // Blend existing color toward new stamp color, weighted by stamp strength vs existing.
                float blendFactor = stampWeight / (stampWeight + existingAlpha + 0.001);
                float3 outColor = lerp(existingColor, newColor, blendFactor);

                // Alpha pushes toward 1 with each stamp (saturates, never overshoots).
                float outAlpha = saturate(existingAlpha + stampWeight * (1.0 - existingAlpha));

                return float4(outColor, outAlpha);
            }
            ENDCG
        }
    }
}
