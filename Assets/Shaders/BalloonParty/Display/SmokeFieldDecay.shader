Shader "BalloonParty/Display/PaintingFieldDecay"
{
    // Per-tick decay + smoke dispersion blit for the painting field. Each tick:
    // 1. Wind advection (semi-Lagrangian): shifts paint in wind direction
    // 2. Turbulent perturbation: per-pixel random nudge so wisps diverge
    // 3. Diffusion expansion: paint spreads outward into adjacent empty texels
    // 4. Noise-modulated erosion: ragged, wispy edge breakup (not smooth shrink)
    // 5. Linear alpha decay: overall opacity loss
    Properties
    {
        [HideInInspector] _MainTex ("Source (read RT)", 2D) = "black" {}
        _DecayRate          ("Decay Rate",              Float)              = 0.08
        _ErosionRate        ("Erosion Rate",            Float)              = 0.12
        _ExpansionRate      ("Expansion Rate",          Range(0, 0.3))      = 0.06
        _WindDir            ("Wind Direction",          Vector)             = (0.3, 0.1, 0, 0)
        _WindSpeed          ("Wind Speed",              Float)              = 0.4
        _TurbAdvectStrength ("Turb Advect Strength",    Range(0, 0.003))    = 0.0008
        _TurbAdvectFreq     ("Turb Advect Freq",        Float)              = 8.0
        _NoiseErosionFreq   ("Noise Erosion Freq",      Float)              = 12.0
        _NoiseErosionContrast ("Noise Contrast",        Range(1, 6))        = 3.0
        _TimePhase          ("Time Phase",              Float)              = 0.0
        _DeltaTime          ("Delta Time",              Float)              = 0.05
        _WindAgeBias        ("Wind Age Bias",           Range(0.1, 4))      = 1.5
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
            float _ExpansionRate;
            float4 _WindDir;
            float _WindSpeed;
            float _TurbAdvectStrength;
            float _TurbAdvectFreq;
            float _NoiseErosionFreq;
            float _NoiseErosionContrast;
            float _TimePhase;
            float _DeltaTime;
            float _WindAgeBias;

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
                // Pre-sample to determine age: fresh paint (high alpha) resists wind.
                float preAlpha = tex2D(_MainTex, i.uv).a;
                float ageWindFactor = pow(1.0 - saturate(preAlpha), _WindAgeBias);

                // 1. Wind advection: sample from upstream so paint flows downwind.
                float2 windOffset = _WindDir.xy * _WindSpeed * _DeltaTime * ageWindFactor;

                // 2. Turbulent perturbation: hash-based per-pixel nudge.
                float2 noiseIn = i.uv * _TurbAdvectFreq + float2(_TimePhase * 0.37, _TimePhase * 0.13);
                float nx = frac(sin(dot(noiseIn,       float2(127.1, 311.7))) * 43758.55);
                float ny = frac(sin(dot(noiseIn + 0.5, float2(269.5, 183.3))) * 27385.23);
                float2 turbOffset = (float2(nx, ny) - 0.5) * 2.0 * _TurbAdvectStrength * _DeltaTime * ageWindFactor;

                float2 advUV = i.uv - windOffset - turbOffset;

                float4 data = tex2D(_MainTex, advUV);
                float3 color = data.rgb;
                float alpha = data.a;

                // 3. Neighbor sampling for expansion + erosion.
                float2 tx = _MainTex_TexelSize.xy;
                float4 nN = tex2D(_MainTex, advUV + float2(0,     tx.y));
                float4 nS = tex2D(_MainTex, advUV + float2(0,    -tx.y));
                float4 nE = tex2D(_MainTex, advUV + float2(tx.x,  0));
                float4 nW = tex2D(_MainTex, advUV + float2(-tx.x, 0));
                float avgNeighborA = (nN.a + nS.a + nE.a + nW.a) * 0.25;

                // 4. Diffusion expansion: empty texels next to paint receive color.
                float expansionFill = max(0.0, avgNeighborA - alpha) * _ExpansionRate * _DeltaTime * 60.0;
                float3 neighborColorSum = nN.rgb * nN.a + nS.rgb * nS.a + nE.rgb * nE.a + nW.rgb * nW.a;
                float neighborAlphaSum = nN.a + nS.a + nE.a + nW.a;
                float3 expandColor = neighborAlphaSum > 0.001
                    ? neighborColorSum / neighborAlphaSum
                    : color;
                color = lerp(color, expandColor, expansionFill / max(alpha + expansionFill, 0.001));
                alpha = alpha + expansionFill;

                // 5. Noise-modulated erosion: ragged wispy edges.
                float2 noiseUV = advUV * _NoiseErosionFreq;
                float noiseA = frac(sin(dot(noiseUV,                    float2(127.1, 311.7))) * 43758.55);
                float noiseB = frac(sin(dot(noiseUV * 1.7 + 0.5,       float2(269.5, 183.3))) * 27385.23);
                float noiseProduct = noiseA * noiseB;

                float2 animNoise = advUV * _NoiseErosionFreq + float2(_TimePhase * 0.2, _TimePhase * 0.07);
                float noiseC = frac(sin(dot(animNoise,                  float2(127.1, 311.7))) * 43758.55);
                float noiseD = frac(sin(dot(animNoise * 1.4 + 0.8,     float2(53.7, 251.1))) * 19483.29);
                float erosionNoise = lerp(noiseProduct, noiseC * noiseD, 0.4);
                float sharpNoise = saturate((erosionNoise - 0.25) * _NoiseErosionContrast + 0.25);

                float edgeness = 1.0 - saturate(avgNeighborA / max(alpha, 0.001));
                float erosion = edgeness * _ErosionRate * _DeltaTime * (0.2 + sharpNoise * 1.6);

                // 6. Linear decay + erosion.
                alpha = max(0.0, alpha - _DecayRate * _DeltaTime - erosion);

                // Clear color when invisible.
                color *= step(0.001, alpha);

                return float4(color, alpha);
            }
            ENDCG
        }
    }
}
