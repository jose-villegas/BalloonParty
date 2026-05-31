Shader "Hidden/BalloonParty/Grid/PuffCloudStamp"
{
    // ── Disturbance stamp pass ───────────────────────────────────────────
    // Subtracts a radial falloff from the density field at _StampCenter.
    // An optional directional wake elongates the stamp along _StampDirection,
    // producing a teardrop-shaped hole that trails behind a moving object.
    //
    // Used by PuffCloudView when a disturbance event occurs.
    // ─────────────────────────────────────────────────────────────────────
    Properties
    {
        _MainTex        ("Density (read)", 2D)    = "white" {}
        _StampCenter    ("Stamp Center UV", Vector) = (0.5, 0.5, 0, 0)
        _StampRadius    ("Stamp Radius UV", Float)  = 0.1
        _StampStrength  ("Stamp Strength",  Float)  = 0.8
        _StampDirection ("Stamp Direction", Vector) = (0, 0, 0, 0)
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
                float current = tex2D(_MainTex, uv).r;

                // Radial falloff from stamp center
                float dist = length(uv - _StampCenter.xy);
                float falloff = smoothstep(_StampRadius, 0.0, dist);

                // Directional wake — elongate in the travel direction
                float2 dir = _StampDirection.xy;
                float dirLen = length(dir);
                if (dirLen > 0.001)
                {
                    float2 toCenter = uv - _StampCenter.xy;
                    float along = dot(toCenter, dir / dirLen);
                    float wake = smoothstep(_StampRadius * 1.5, 0.0, abs(along))
                               * step(0.0, along);
                    falloff = max(falloff, wake * 0.5);
                }

                float result = max(0.0, current - falloff * _StampStrength);
                return fixed4(result, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
}

