Shader "Hidden/BalloonParty/Grid/DisturbanceStampBatched"
{
    // Processes up to 32 stamps in a single blit to avoid per-stamp RT
    // ping-pong overhead. Each stamp has a center, radius, strength, and
    // direction packed into uniform arrays.
    // Used as a standalone fallback when stamps arrive without a diffusion
    // tick. The combined path (_STAMPS_ON in DisturbanceDiffusion) is
    // preferred when both run in the same frame.
    Properties
    {
        _MainTex        ("Field (read)",    2D)    = "white" {}
        _DisplaceAmount ("Displace Amount", Float) = 0.3
        _StampCount     ("Stamp Count",     Int)   = 0
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

            #define MAX_STAMPS 32

            sampler2D _MainTex;
            float     _DisplaceAmount;
            int       _StampCount;

            float4 _StampCenters[MAX_STAMPS];   // xy = UV center
            float  _StampRadii[MAX_STAMPS];
            float  _StampStrengths[MAX_STAMPS];
            float4 _StampDirections[MAX_STAMPS]; // xy = direction
            float  _StampColorIndices[MAX_STAMPS]; // encoded palette index; 0 = no color

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
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
                o.uv     = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 current = tex2D(_MainTex, uv);

                float density = current.r;
                float2 displace = current.gb;
                // Palette indices are written hard, never blended — averaging two would be a wrong color.
                float colorIndex = current.a;

                for (int s = 0; s < _StampCount; s++)
                {
                    float2 center = _StampCenters[s].xy;
                    float  radius = _StampRadii[s];
                    float  strength = _StampStrengths[s];
                    float2 dir = _StampDirections[s].xy;

                    float2 toPixel = uv - center;
                    float dist = length(toPixel);

                    // Radial falloff
                    float falloff = smoothstep(radius, 0.0, dist);

                    // Directional wake trail
                    float dirLen = length(dir);
                    if (dirLen > 0.001)
                    {
                        float2 dirNorm = dir / dirLen;
                        float along = dot(toPixel, dirNorm);
                        float perpDist = length(toPixel - along * dirNorm);
                        float wakeAlong = smoothstep(radius * 2.5, 0.0, -along)
                                        * step(along, 0.0);
                        float wakePerp = smoothstep(radius * 0.6, 0.0, perpDist);
                        falloff = max(falloff, wakeAlong * wakePerp * 0.5);
                    }

                    // Density subtraction
                    density = max(0.0, density - falloff * strength);

                    // Displacement
                    float2 pushDir;
                    if (dirLen > 0.001)
                    {
                        pushDir = -(dir / dirLen);
                    }
                    else
                    {
                        pushDir = (dist > 0.001) ? -(toPixel / dist) : float2(0, 0);
                    }
                    displace += pushDir * falloff * _DisplaceAmount;

                    float encoded = _StampColorIndices[s];
                    if (encoded > 0.001 && falloff > 0.2)
                    {
                        colorIndex = encoded;
                    }
                }

                return fixed4(density, saturate(displace), colorIndex);
            }
            ENDCG
        }
    }
}


