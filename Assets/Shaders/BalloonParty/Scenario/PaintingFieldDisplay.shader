Shader "BalloonParty/Scenario/PaintingFieldDisplay"
{
    // Full-screen display of the painting field RT as a watercolor wash behind the cloud backdrop.
    // Placed on a quad spanning the viewport, sortingOrder below the clouds. It reads the global
    // _PaintingTex (pushed by PaintingFieldService) and applies a suite of watercolor techniques:
    // fBM-warped edges, anisotropic bleed, soft-light paper grain, pigment pooling, warm/cool
    // temperature tint, shadow lift, and optional Voronoi granulation. Premultiplied alpha output.
    Properties
    {
        _Color              ("Tint",                    Color)              = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color)          = (1, 1, 1, 1)

        [Header(Bleed and Diffusion)]
        _Opacity            ("Opacity",                 Range(0, 1))        = 0.8
        _BleedRadius        ("Bleed Radius",            Range(0, 0.02))     = 0.005
        _TurbStrength       ("Turbulence Strength",     Range(0, 0.008))    = 0.003
        _TurbFreq           ("Turbulence Frequency",    Float)              = 6.0

        [Header(Swirl)]
        _SwirlFreq          ("Swirl Frequency",         Float)              = 2.5
        _SwirlStrength      ("Swirl Strength",          Range(0, 0.012))    = 0.004
        _SwirlSpeed         ("Swirl Speed",             Range(0, 0.3))      = 0.04

        [Header(Edges)]
        _EdgeSoftness       ("Edge Softness",           Range(0.01, 1))     = 0.3
        _SmokeEdgeSharpness ("Smoke Edge Sharpness",    Range(1, 10))       = 4.0
        _EdgeWarpStrength   ("Edge Warp Strength",      Range(0, 0.02))     = 0.006
        _EdgeWarpFreq       ("Edge Warp Frequency",     Float)              = 3.5
        _WispNoiseFreq      ("Wisp Noise Freq",         Float)              = 5.0
        _WispStrength       ("Wisp Strength",           Range(0, 1))        = 0.6

        [Header(Paper Grain)]
        _GrainTex           ("Paper Grain",             2D)                 = "gray" {}
        _GrainStrength      ("Grain Strength",          Range(0, 1))        = 0.2
        _GrainScale         ("Grain Scale",             Float)              = 8.0

        [Header(Internal Density)]
        _InternalDensityScale  ("Density Noise Scale",    Float)          = 3.0
        _InternalDensityStrength ("Density Strength",     Range(0, 1))    = 0.4

        [Header(Age Gradient)]
        _AgeGradientStrength   ("Age Turbulence Boost",   Range(0, 3))    = 1.0

        [Header(Atmosphere)]
        _SkyTransmissionColor ("Sky Transmission",      Color)              = (0.7, 0.85, 1.0, 1)
        _SkyTransmissionStrength ("Transmission Strength", Range(0, 1))     = 0.5
        _ShadowLiftColor    ("Shadow Lift Color",       Color)              = (0.88, 0.92, 0.98, 1)

        [Header(Lighting)]
        _LightResponse      ("Light Response",          Range(0, 1))        = 0.6
        _LightGlow          ("Local Light Glow",        Range(0, 1))        = 0.3
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/PaintingField.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Noise/SimplexNoise2D.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 worldPos : TEXCOORD0;
            };

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;
            float  _Opacity;
            float  _BleedRadius;
            float  _TurbStrength;
            float  _TurbFreq;
            float  _SwirlFreq;
            float  _SwirlStrength;
            float  _SwirlSpeed;
            float  _EdgeSoftness;
            float  _SmokeEdgeSharpness;
            float  _EdgeWarpStrength;
            float  _EdgeWarpFreq;
            float  _WispNoiseFreq;
            float  _WispStrength;
            sampler2D _GrainTex;
            float  _GrainStrength;
            float  _GrainScale;
            float  _InternalDensityScale;
            float  _InternalDensityStrength;
            float  _AgeGradientStrength;
            fixed4 _SkyTransmissionColor;
            float  _SkyTransmissionStrength;
            fixed4 _ShadowLiftColor;
            float  _LightResponse;
            float  _LightGlow;
            float  _PaintingTime;

            // ────────────────────────────────────────────────────────────────
            // Helpers
            // ────────────────────────────────────────────────────────────────

            // Wet-on-wet turbulence: jitter sample position with simplex noise for dreamy diffusion.
            float2 TurbulentOffset(float2 wp)
            {
                float tx = SimplexNoise2D(wp * _TurbFreq);
                float ty = SimplexNoise2D(wp * _TurbFreq + float2(3.3, 7.1));
                return float2(tx, ty) * _TurbStrength;
            }

            // Curl noise: divergence-free flow field that produces pure swirl (no stretching).
            // Finite-difference curl of a scalar simplex noise field.
            float2 CurlNoise2D(float2 p)
            {
                const float eps = 0.003;
                float n_py = SimplexNoise2D(float2(p.x, p.y + eps));
                float n_my = SimplexNoise2D(float2(p.x, p.y - eps));
                float n_px = SimplexNoise2D(float2(p.x + eps, p.y));
                float n_mx = SimplexNoise2D(float2(p.x - eps, p.y));
                return float2(n_py - n_my, -(n_px - n_mx)) / (2.0 * eps);
            }

            // 3-octave curl noise: large slow eddies + medium body + small domain-warped wisps.
            float2 FlowOffset(float2 wp)
            {
                float paintDensity = PaintingFieldSample(wp).a;

                // Octave 1: large slow eddies — overall trail bow.
                float2 p1 = wp * (_SwirlFreq * 0.25)
                          + float2(_PaintingTime * _SwirlSpeed * 0.25, _PaintingTime * _SwirlSpeed * 0.12);
                float2 curl1 = CurlNoise2D(p1) * (_SwirlStrength * 3.0);

                // Octave 2: medium body undulation.
                float2 p2 = wp * _SwirlFreq
                          + float2(_PaintingTime * _SwirlSpeed * 0.7, _PaintingTime * _SwirlSpeed * 0.4);
                float2 curl2 = CurlNoise2D(p2) * _SwirlStrength;

                // Octave 3: small wisps, domain-warped by curl1 for detachment.
                float2 p3 = (wp + curl1 * 0.4) * (_SwirlFreq * 3.5)
                          + float2(_PaintingTime * _SwirlSpeed * 2.8, _PaintingTime * _SwirlSpeed * 1.9);
                float2 curl3 = CurlNoise2D(p3) * (_SwirlStrength * 0.3);

                // Age gradient: old smoke (low alpha, still present) swirls more aggressively.
                float hasSmoke = step(0.01, paintDensity);
                float ageBoost = (1.0 - paintDensity) * hasSmoke * _AgeGradientStrength;
                float denseScale = (0.2 + paintDensity * 0.8) * (1.0 + ageBoost);
                float wispScale = (1.0 - paintDensity * 0.5) * (1.0 + ageBoost * 0.5);

                return curl1 * denseScale + curl2 * denseScale + curl3 * (denseScale + wispScale * 0.5);
            }

            // fBM-warped position for organic edge alpha. Two octaves of simplex warp the lookup
            // so the transparency boundary wiggles like wet paper, not a perfect circle.
            float2 WarpedEdgePos(float2 wp)
            {
                float f = _EdgeWarpFreq;
                float nx = SimplexNoise2D(wp * f);
                float ny = SimplexNoise2D(wp * f + float2(5.2, 1.3));
                float nx2 = SimplexNoise2D(wp * f * 2.1 + float2(1.7, 9.2)) * 0.4;
                float ny2 = SimplexNoise2D(wp * f * 2.1 + float2(8.3, 2.8)) * 0.4;
                float2 warp = float2(nx + nx2, ny + ny2) * _EdgeWarpStrength;
                return wp + warp;
            }

            // 5-tap bleed: center + cardinal directions with alpha-driven radius.
            float4 SampleBleeded(float2 wp)
            {
                float2 baseUV = PaintingFieldUV(wp);
                float4 raw = tex2D(_PaintingTex, baseUV);
                float4 center = float4(raw.rgb, raw.a * _PaintingFieldActive);
                float r = _BleedRadius * (0.4 + center.a * 1.2);
                float2 rUV = float2(r, 0) / max(_PaintingBoundsSize, 1e-4);

                float4 s0 = tex2D(_PaintingTex, baseUV + float2( rUV.x, 0));
                float4 s1 = tex2D(_PaintingTex, baseUV + float2(-rUV.x, 0));
                float4 s2 = tex2D(_PaintingTex, baseUV + float2(0,  rUV.x * 1.3));
                float4 s3 = tex2D(_PaintingTex, baseUV + float2(0, -rUV.x));
                s0.a *= _PaintingFieldActive;
                s1.a *= _PaintingFieldActive;
                s2.a *= _PaintingFieldActive;
                s3.a *= _PaintingFieldActive;

                return center * 0.45 + (s0 + s1 + s2 + s3) * 0.1375;
            }

            // Photoshop soft-light blend — models how pigment settles into paper grain valleys
            // while leaving fiber peaks lighter.
            float SoftLight(float base, float blend)
            {
                float dark  = 2.0 * base * blend + base * base * (1.0 - 2.0 * blend);
                float light = sqrt(base) * (2.0 * blend - 1.0) + 2.0 * base * (1.0 - blend);
                return blend < 0.5 ? dark : light;
            }

            // ────────────────────────────────────────────────────────────────
            // Vertex / Fragment
            // ────────────────────────────────────────────────────────────────

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.color    = IN.color * _Color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 0. Early discard: skip the vast majority of empty pixels cheaply.
                float2 rawUV = PaintingFieldUV(IN.worldPos);
                float quickAlpha = tex2D(_PaintingTex, rawUV).a * _PaintingFieldActive;
                if (quickAlpha < 0.001)
                {
                    discard;
                }

                // 1. Turbulent jitter + 3-octave curl noise swirl.
                float2 wp = IN.worldPos + TurbulentOffset(IN.worldPos) + FlowOffset(IN.worldPos);

                // 2. Anisotropic alpha-weighted bleed (5-tap: center + cardinal).
                float4 paint = SampleBleeded(wp);

                if (paint.a < 0.001)
                {
                    discard;
                }

                // 3. Smoke edge shaping: sigmoid core + wisp noise at edges.
                float2 warpedWp = WarpedEdgePos(wp);
                float warpedA = PaintingFieldSample(warpedWp).a;
                float coreAlpha = saturate(warpedA / max(_EdgeSoftness, 0.001));
                float smokeSigmoid = coreAlpha / (coreAlpha + (1.0 - coreAlpha) * _SmokeEdgeSharpness);

                float edgeRegion = 1.0 - saturate(warpedA * 4.0);
                float2 wispNoiseUV = warpedWp * _WispNoiseFreq
                                   + float2(_PaintingTime * 0.04, _PaintingTime * 0.02);
                float wispNoise = SimplexNoise2D(wispNoiseUV) * 0.5 + 0.5;
                float wispMask = smoothstep(0.3, 0.5, wispNoise);
                float edgeFade = lerp(smokeSigmoid, smokeSigmoid * wispMask, edgeRegion * _WispStrength);

                // 4. Paper grain with soft-light blending.
                float2 grainUV_coarse = wp * _GrainScale;
                float2 grainUV_fine   = wp * _GrainScale * 4.7;
                float grainCoarse = tex2D(_GrainTex, grainUV_coarse).r;
                float grainFine   = tex2D(_GrainTex, grainUV_fine).g;
                float grain = grainCoarse * 0.7 + grainFine * 0.3;
                float grainBlend = lerp(0.5, grain, _GrainStrength);

                float3 finalRgb = float3(
                    SoftLight(paint.r, grainBlend),
                    SoftLight(paint.g, grainBlend),
                    SoftLight(paint.b, grainBlend)
                );
                float grainAlphaMod = lerp(1.0, grain * 0.8 + 0.2, _GrainStrength * 0.4);

                // 5. Sky transmission: edges lighten toward sky color (smoke thins out).
                float densityFalloff = pow(1.0 - saturate(paint.a * 2.0), 2.0);
                float3 transmitted = lerp(finalRgb, _SkyTransmissionColor.rgb * finalRgb,
                                          densityFalloff * _SkyTransmissionStrength);
                float rimLight = densityFalloff * 0.3 * _SkyTransmissionStrength;
                finalRgb = transmitted + rimLight;

                // 6. Shadow lift — smoke never goes fully black.
                float lum = dot(finalRgb, float3(0.299, 0.587, 0.114));
                finalRgb = lerp(_ShadowLiftColor.rgb * lum, finalRgb, saturate(lum * 3.0));

                finalRgb *= IN.color.rgb;

                // 7. Scene light interaction: smoke is lit by the scene's lights.
                float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1, 1, 1);
                float ambient = SceneLightAmbientMagnitude();
                float3 sceneTint = keyColor * ambient;
                float3 localGlow = float3(0, 0, 0);
                if (_SceneLightFieldOn > 0.5)
                {
                    float4 lightTap = SceneLightFieldSample(IN.worldPos);
                    float local = lightTap.r;
                    float2 lightUV = SceneLightFieldUV(IN.worldPos);
                    float3 palette = SceneLightPaletteColorAt(lightUV, keyColor);
                    sceneTint = lerp(keyColor, palette, saturate(local)) * (ambient + local);
                    localGlow = palette * local;
                }
                finalRgb *= lerp(float3(1, 1, 1), sceneTint, _LightResponse);
                finalRgb += localGlow * _LightGlow * paint.a;

                // 8. Interior density modulation: animated see-through patches inside body.
                float2 densityUV = wp * _InternalDensityScale
                                 + float2(_PaintingTime * 0.03, _PaintingTime * -0.02);
                float densityNoise = tex2D(_GrainTex, densityUV).r;
                float bodyRegion = saturate(paint.a * 3.0 - 0.5);
                float densityMod = 1.0 - bodyRegion * densityNoise * _InternalDensityStrength;

                float finalAlpha = paint.a * _Opacity * grainAlphaMod * edgeFade
                                 * densityMod * IN.color.a;

                // Premultiplied alpha output (Blend One OneMinusSrcAlpha).
                return fixed4(finalRgb * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
