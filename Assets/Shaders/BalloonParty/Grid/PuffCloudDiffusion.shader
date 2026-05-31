Shader "Hidden/BalloonParty/Grid/PuffCloudDiffusion"
{
    // ── Density field diffusion pass ──────────────────────────────────────
    // Blits the density RT through a 3×3 Gaussian blur weighted toward
    // equilibrium (1.0). Two forces act on each texel:
    //   1. Spatial smoothing — _DiffusionRate blends toward the 3×3 average
    //   2. Temporal recovery — _ReformSpeed trends toward 1.0 over time
    //
    // Used by PuffCloudView in a ping-pong blit each diffusion tick.
    // ─────────────────────────────────────────────────────────────────────
    Properties
    {
        _MainTex       ("Density (read)", 2D) = "white" {}
        _DiffusionRate ("Diffusion Rate", Range(0, 1)) = 0.15
        _ReformSpeed   ("Reform Speed",   Range(0, 2)) = 0.4
        _DeltaTime     ("Delta Time",     Float)       = 0.05
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
            float4    _MainTex_TexelSize;
            float     _DiffusionRate;
            float     _ReformSpeed;
            float     _DeltaTime;

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
                float2 tx = _MainTex_TexelSize.xy;

                float current = tex2D(_MainTex, uv).r;

                // 3×3 Gaussian-weighted average (sigma ≈ 0.85)
                //   1  2  1
                //   2  4  2   / 16
                //   1  2  1
                float tl = tex2D(_MainTex, uv + float2(-tx.x,  tx.y)).r;
                float tc = tex2D(_MainTex, uv + float2(  0.0,  tx.y)).r;
                float tr = tex2D(_MainTex, uv + float2( tx.x,  tx.y)).r;
                float ml = tex2D(_MainTex, uv + float2(-tx.x,   0.0)).r;
                float mr = tex2D(_MainTex, uv + float2( tx.x,   0.0)).r;
                float bl = tex2D(_MainTex, uv + float2(-tx.x, -tx.y)).r;
                float bc = tex2D(_MainTex, uv + float2(  0.0, -tx.y)).r;
                float br = tex2D(_MainTex, uv + float2( tx.x, -tx.y)).r;

                float blurred = (tl + tr + bl + br)
                              + (tc + ml + mr + bc) * 2.0
                              + current * 4.0;
                blurred /= 16.0;

                // Spatial smoothing
                float result = lerp(current, blurred, _DiffusionRate);

                // Temporal recovery toward equilibrium (1.0)
                result = lerp(result, 1.0, _ReformSpeed * _DeltaTime);

                return fixed4(result, 0.0, 0.0, 1.0);
            }
            ENDCG
        }
    }
}

