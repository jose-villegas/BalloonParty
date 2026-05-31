Shader "Hidden/BalloonParty/Grid/DisturbanceDiffusion"
{
    // ── Density + displacement diffusion pass ────────────────────────────
    // Processes a packed RT where:
    //   R = density (equilibrium = 1.0)
    //   G = displacement X (equilibrium = 0.5, i.e. zero offset)
    //   B = displacement Y (equilibrium = 0.5)
    //
    // Three forces per channel:
    //   1. Advection — wind shifts sample origin for directional flow
    //   2. Pressure diffusion — high→low flow fills holes from edges
    //   3. Equilibrium restoration — gentle nudge toward rest state
    //
    // Displacement decays faster than density so the cloud snaps back
    // to shape before fully reforming in opacity.
    // ─────────────────────────────────────────────────────────────────────
    Properties
    {
        _MainTex          ("Field (read)",      2D)          = "white" {}
        _DiffusionRate    ("Diffusion Rate",    Range(0, 1)) = 0.3
        _ReformSpeed      ("Reform Speed",      Range(0, 0.5)) = 0.05
        _DisplaceDecay    ("Displace Decay",    Range(0, 3)) = 1.5
        _DeltaTime        ("Delta Time",        Float)       = 0.05
        _WindDir          ("Wind Direction",     Vector)      = (0.3, 0.1, 0, 0)
        _WindSpeed        ("Wind Speed",         Float)       = 1.0
        _PressureStr      ("Pressure Strength",  Range(0, 1)) = 0.4
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
            float     _DisplaceDecay;
            float     _DeltaTime;
            float4    _WindDir;
            float     _WindSpeed;
            float     _PressureStr;

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

                // Semi-Lagrangian advection — sample from upstream of wind
                float2 windOffset = _WindDir.xy * _WindSpeed * _DeltaTime;
                float2 advUV = uv - windOffset;

                float3 current = tex2D(_MainTex, uv).rgb;

                // 3×3 samples at advected origin (all 3 channels)
                float3 stl = tex2D(_MainTex, advUV + float2(-tx.x,  tx.y)).rgb;
                float3 stc = tex2D(_MainTex, advUV + float2(  0.0,  tx.y)).rgb;
                float3 str = tex2D(_MainTex, advUV + float2( tx.x,  tx.y)).rgb;
                float3 sml = tex2D(_MainTex, advUV + float2(-tx.x,   0.0)).rgb;
                float3 smc = tex2D(_MainTex, advUV).rgb;
                float3 smr = tex2D(_MainTex, advUV + float2( tx.x,   0.0)).rgb;
                float3 sbl = tex2D(_MainTex, advUV + float2(-tx.x, -tx.y)).rgb;
                float3 sbc = tex2D(_MainTex, advUV + float2(  0.0, -tx.y)).rgb;
                float3 sbr = tex2D(_MainTex, advUV + float2( tx.x, -tx.y)).rgb;

                // Gaussian blur of advected neighborhood
                float3 blurred = (stl + str + sbl + sbr)
                               + (stc + sml + smr + sbc) * 2.0
                               + smc * 4.0;
                blurred /= 16.0;

                // Pressure gradient on density (R channel)
                float maxN = max(max(max(stl.r, stc.r), max(str.r, sml.r)),
                                 max(max(smr.r, sbl.r), max(sbc.r, sbr.r)));
                float pressure = max(0.0, maxN - current.r) * _PressureStr;

                // Spatial diffusion
                float3 result = lerp(current, blurred, _DiffusionRate);

                // Pressure fill on density
                result.r += pressure * _DeltaTime;

                // Equilibrium restoration:
                //   density → 1.0 (slow)
                //   displacement → 0.5 (faster, so shape snaps back before opacity)
                result.r = lerp(result.r, 1.0, _ReformSpeed * _DeltaTime);
                result.gb = lerp(result.gb, float2(0.5, 0.5), _DisplaceDecay * _DeltaTime);

                return fixed4(saturate(result), 1.0);
            }
            ENDCG
        }
    }
}

