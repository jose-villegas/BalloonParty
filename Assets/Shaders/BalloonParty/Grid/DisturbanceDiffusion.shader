Shader "Hidden/BalloonParty/Grid/DisturbanceDiffusion"
{
    // ── Density + displacement diffusion pass ────────────────────────────
    // Processes a packed RT where:
    //   R = signed density (equilibrium = 0.5): > 0.5 repels specks, < 0.5 attracts them
    //   G = displacement X (equilibrium = 0.5, i.e. zero offset)
    //   B = displacement Y (equilibrium = 0.5)
    //   A = palette tag, packed (index + life) / 16 with life in (0, 1]: 16 index slots, and the
    //       value's position within its slot is the tag's remaining life. A fresh stamp writes the
    //       slot's top ((index + 1) / 16); each diffusion tick drains the life until the tag zeroes.
    //       Written hard, never diffused/blended (averaging two indices would be a wrong color).
    //
    // Three forces per channel:
    //   1. Advection — wind shifts sample origin for directional flow
    //   2. Pressure diffusion — high→low flow fills holes from edges
    //   3. Equilibrium restoration — gentle nudge toward rest state
    //
    // Displacement decays faster than density so the cloud snaps back
    // to shape before fully reforming in opacity.
    //
    // When _STAMPS_ON is enabled, pending stamps are applied after
    // diffusion in the same pass — eliminating a separate stamp blit.
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
        _DisplaceAmount   ("Displace Amount",    Float)       = 0.3
        _StampCount       ("Stamp Count",        Int)         = 0
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
            #pragma multi_compile _ _STAMPS_ON
            #include "UnityCG.cginc"

            #define MAX_STAMPS 32

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _DiffusionRate;
            float     _ReformSpeed;
            float     _DisplaceDecay;
            float     _DeltaTime;
            float4    _WindDir;
            float     _WindSpeed;
            float     _PressureStr;
            float     _ColorDecay;

            #if _STAMPS_ON
            float     _DisplaceAmount;
            float     _StampAspect; // field height/width; corrects UV anisotropy so stamps stay circular
            int       _StampCount;
            float4    _StampCenters[MAX_STAMPS];
            float     _StampRadii[MAX_STAMPS];
            float     _StampStrengths[MAX_STAMPS];
            float4    _StampDirections[MAX_STAMPS];
            float     _StampColorIndices[MAX_STAMPS];
            #endif

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

                float4 current4 = tex2D(_MainTex, uv);
                float3 current = current4.rgb;

                // Drain the palette tag's in-slot life; the tag dies (0) once it bottoms out.
                float colorIndex = current4.a;
                float tagValue = colorIndex * 16.0;
                if (tagValue > 0.05)
                {
                    float slot = ceil(tagValue) - 1.0;
                    float life = tagValue - slot - _ColorDecay * _DeltaTime;
                    colorIndex = life <= 0.05 ? 0.0 : (slot + life) / 16.0;
                }
                else
                {
                    colorIndex = 0.0;
                }

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
                //   density → 0.5 (slow) — the signed rest: above repels, below attracts
                //   displacement → 0.5 (faster, so shape snaps back before opacity)
                result.r = lerp(result.r, 0.5, _ReformSpeed * _DeltaTime);
                result.gb = lerp(result.gb, float2(0.5, 0.5), _DisplaceDecay * _DeltaTime);

                #if _STAMPS_ON
                float density = result.r;
                float2 displace = result.gb;

                for (int s = 0; s < _StampCount; s++)
                {
                    float2 center = _StampCenters[s].xy;
                    float  radius = _StampRadii[s];
                    float  strength = _StampStrengths[s];
                    float2 dir = _StampDirections[s].xy;

                    // Correct the per-axis UV normalisation so the falloff is circular in world space.
                    float2 toPixel = uv - center;
                    toPixel.y *= _StampAspect;
                    float dist = length(toPixel);

                    float falloff = smoothstep(radius, 0.0, dist);

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

                    // Signed density around the 0.5 rest: strength > 0 bumps up (repulsion),
                    // strength < 0 digs down (attraction).
                    density = saturate(density + falloff * strength);

                    float2 pushDir;
                    if (dirLen > 0.001)
                    {
                        pushDir = -(dir / dirLen);
                    }
                    else
                    {
                        pushDir = (dist > 0.001) ? -(toPixel / dist) : float2(0, 0);
                    }
                    // Flow follows the sign: repulsion (+) pushes specks out, attraction (-) pulls
                    // them in, color-only (0) shifts nothing.
                    displace += pushDir * falloff * _DisplaceAmount * sign(strength);

                    float encoded = _StampColorIndices[s];
                    if (encoded > 0.001 && falloff > 0.2)
                    {
                        colorIndex = encoded;
                    }
                }

                result = float3(density, saturate(displace));
                #endif

                return fixed4(saturate(result), colorIndex);
            }
            ENDCG
        }
    }
}

