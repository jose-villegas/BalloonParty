Shader "Hidden/BalloonParty/Grid/PuffCloudStamp"
{
    // ── Disturbance stamp pass ───────────────────────────────────────────
    // Packed RT: R=density, G=displacement X (0.5=zero), B=displacement Y.
    //
    // On stamp:
    //   - Density is subtracted (radial falloff) — creates an opacity hole
    //   - Displacement is pushed in the stamp direction — the noise
    //     sampling coordinates warp, visibly deforming the cloud shape
    //
    // _StampDirection comes from the drag/projectile velocity and controls
    // which way cloud matter gets pushed aside.
    // ─────────────────────────────────────────────────────────────────────
    Properties
    {
        _MainTex         ("Field (read)",     2D)     = "white" {}
        _StampCenter     ("Stamp Center UV",  Vector) = (0.5, 0.5, 0, 0)
        _StampRadius     ("Stamp Radius UV",  Float)  = 0.1
        _StampStrength   ("Stamp Strength",   Float)  = 0.8
        _StampDirection  ("Stamp Direction",  Vector) = (0, 0, 0, 0)
        _DisplaceAmount  ("Displace Amount",  Float)  = 0.3
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

            sampler2D _MainTex;
            float4    _StampCenter;
            float     _StampRadius;
            float     _StampStrength;
            float4    _StampDirection;
            float     _DisplaceAmount;

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
                float3 current = tex2D(_MainTex, uv).rgb;

                float2 toPixel = uv - _StampCenter.xy;
                float dist = length(toPixel);

                // Radial falloff
                float falloff = smoothstep(_StampRadius, 0.0, dist);

                // Directional wake trail
                float2 dir = _StampDirection.xy;
                float dirLen = length(dir);
                if (dirLen > 0.001)
                {
                    float2 dirNorm = dir / dirLen;
                    float along = dot(toPixel, dirNorm);
                    float perpDist = length(toPixel - along * dirNorm);
                    float wakeAlong = smoothstep(_StampRadius * 2.5, 0.0, -along)
                                    * step(along, 0.0);
                    float wakePerp = smoothstep(_StampRadius * 0.6, 0.0, perpDist);
                    falloff = max(falloff, wakeAlong * wakePerp * 0.5);
                }

                // Density subtraction
                float density = max(0.0, current.r - falloff * _StampStrength);

                // Displacement — push cloud matter away from the disturbance.
                // The noise lookup offsets toward the stamp center so the
                // cloud visually expands outward (inverse warp).
                float2 pushDir;
                if (dirLen > 0.001)
                {
                    pushDir = -(dir / dirLen);
                }
                else
                {
                    pushDir = (dist > 0.001) ? -(toPixel / dist) : float2(0, 0);
                }

                // Encode displacement as 0.5-biased: 0.5 = no offset
                float2 displace = current.gb;
                displace += pushDir * falloff * _DisplaceAmount;

                return fixed4(density, saturate(displace), 1.0);
            }
            ENDCG
        }
    }
}
