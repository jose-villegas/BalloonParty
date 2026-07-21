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

        [Header(Edges)]
        _EdgeSoftness       ("Edge Softness",           Range(0.01, 1))     = 0.3
        _EdgePow            ("Edge Curve Power",        Range(0.3, 3))      = 1.8
        _EdgeWarpStrength   ("Edge Warp Strength",      Range(0, 0.02))     = 0.006
        _EdgeWarpFreq       ("Edge Warp Frequency",     Float)              = 3.5
        _PigmentPool        ("Pigment Pooling",         Range(0, 0.5))      = 0.18

        [Header(Paper Grain)]
        _GrainTex           ("Paper Grain",             2D)                 = "gray" {}
        _GrainStrength      ("Grain Strength",          Range(0, 1))        = 0.2
        _GrainScale         ("Grain Scale",             Float)              = 8.0

        [Header(Atmosphere)]
        _WarmCoreColor      ("Warm Core Tint",          Color)              = (1.0, 0.97, 0.88, 1)
        _CoolEdgeColor      ("Cool Edge Tint",          Color)              = (0.82, 0.88, 1.0, 1)
        _TemperatureRange   ("Temperature Range",       Range(0, 1))        = 0.12
        _ShadowLiftColor    ("Shadow Lift Color",       Color)              = (0.88, 0.92, 0.98, 1)
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
            float  _EdgeSoftness;
            float  _EdgePow;
            float  _EdgeWarpStrength;
            float  _EdgeWarpFreq;
            float  _PigmentPool;
            sampler2D _GrainTex;
            float  _GrainStrength;
            float  _GrainScale;
            fixed4 _WarmCoreColor;
            fixed4 _CoolEdgeColor;
            float  _TemperatureRange;
            fixed4 _ShadowLiftColor;

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

            // Anisotropic bleed: 8-tap with alpha-driven radius and slight gravity bias.
            float4 SampleBleeded(float2 wp)
            {
                float4 center = PaintingFieldSample(wp);
                float r = _BleedRadius * (0.4 + center.a * 1.2);

                float4 s0 = PaintingFieldSample(wp + float2( r,        0));
                float4 s1 = PaintingFieldSample(wp + float2(-r,        0));
                float4 s2 = PaintingFieldSample(wp + float2( 0,    r * 1.3));
                float4 s3 = PaintingFieldSample(wp + float2( 0,   -r));
                float4 s4 = PaintingFieldSample(wp + float2( r*0.7,  r*0.7));
                float4 s5 = PaintingFieldSample(wp + float2(-r*0.7,  r*0.7));
                float4 s6 = PaintingFieldSample(wp + float2( r*0.7, -r*0.7));
                float4 s7 = PaintingFieldSample(wp + float2(-r*0.7, -r*0.7));

                return center * 0.35 + (s0+s1+s2+s3) * 0.10 + (s4+s5+s6+s7) * 0.0375;
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
                // 1. Turbulent position jitter for wet-on-wet diffusion haze.
                float2 wp = IN.worldPos + TurbulentOffset(IN.worldPos);

                // 2. Anisotropic alpha-weighted bleed.
                float4 paint = SampleBleeded(wp);

                if (paint.a < 0.001)
                {
                    discard;
                }

                // 3. fBM-warped edge fade — organic, wiggly transparency boundary.
                float warpedA = PaintingFieldSample(WarpedEdgePos(wp)).a;
                float edgeFade = pow(saturate(warpedA / max(_EdgeSoftness, 0.001)), _EdgePow);

                // 4. Two-scale paper grain with soft-light blending.
                float2 grainUV_coarse = wp * _GrainScale;
                float2 grainUV_fine   = wp * _GrainScale * 4.7;
                float grainCoarse = tex2D(_GrainTex, grainUV_coarse).r;
                float grainFine   = tex2D(_GrainTex, grainUV_fine).g;
                float grain = grainCoarse * 0.7 + grainFine * 0.3;
                float grainBlend = lerp(0.5, grain, _GrainStrength);

                float3 grained = float3(
                    SoftLight(paint.r, grainBlend),
                    SoftLight(paint.g, grainBlend),
                    SoftLight(paint.b, grainBlend)
                );
                float grainAlphaMod = lerp(1.0, grain * 0.8 + 0.2, _GrainStrength * 0.4);

                // 5. Pigment pooling — darken the edge band where paint is pushed outward.
                float edgeBand = paint.a * (1.0 - paint.a);
                float poolDarken = 1.0 - edgeBand * _PigmentPool * 4.0;
                grained *= poolDarken;

                // 6. Warm-to-cool temperature tint by density.
                float3 tempTint = lerp(_CoolEdgeColor.rgb, _WarmCoreColor.rgb, paint.a);
                float3 finalRgb = lerp(grained, grained * tempTint, _TemperatureRange);

                // 7. Shadow lift — watercolor never goes fully black; lift toward sky tint.
                float lum = dot(finalRgb, float3(0.299, 0.587, 0.114));
                finalRgb = lerp(_ShadowLiftColor.rgb * lum, finalRgb, saturate(lum * 3.0));

                finalRgb *= IN.color.rgb;

                float finalAlpha = paint.a * _Opacity * grainAlphaMod * edgeFade * IN.color.a;

                // Premultiplied alpha output (Blend One OneMinusSrcAlpha).
                return fixed4(finalRgb * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
