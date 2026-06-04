Shader "BalloonParty/Grid/BushBake"
{
    // Offline canopy baker — renders a full multi-slot bush canopy into a
    // RenderTexture at high quality. NOT used at runtime.
    //
    // Quality features unavailable in the real-time Bush.shader:
    //   • Full Gielis superformula leaf shapes (atan2 + pow per texel)
    //   • Subsurface scattering (Beer-Lambert transmittance)
    //   • Full-depth self-shadow (all layers, Gielis SDF, multi-sample)
    //   • Ambient occlusion (coverage accumulation across all slots/depths)
    //   • Per-leaf colour variation (hue jitter, age gradient, edge browning)
    //   • Enhanced ground shadow (full-depth Gielis + soft penumbra)
    //
    // MPB contract: _SlotCentersWorld[16], _SlotCount — same as ClusterView.
    Properties
    {
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shape)]
        _SlotRadius         ("Slot Radius",          Float)              = 0.40
        _RadiusJitter       ("Radius Jitter",        Range(0, 0.15))     = 0.06
        _AAWidth            ("AA Edge Width",        Range(0.001, 0.03)) = 0.008

        [Header(Canopy)]
        _BranchSpread       ("Spiral Spread",        Range(0.1, 0.8))    = 0.55
        _SubCircleSize      ("Leaf Size",            Range(0.15, 0.7))   = 0.30
        _SubCircleSizeVar   ("Size Variation",       Range(0, 0.5))      = 0.30

        [Header(Gielis Superformula)]
        _GielisM            ("Lobe Count (m)",       Range(0, 6))        = 2.0
        _GielisN1           ("Curvature (n1)",       Range(0.1, 4))      = 1.0
        _GielisN2           ("Lateral Curve (n2)",   Range(0.1, 4))      = 1.5
        _GielisN3           ("Lateral Curve (n3)",   Range(0.1, 4))      = 1.5

        [Header(Surface)]
        _BaseColor          ("Base Color (deep)",    Color)              = (0.14, 0.40, 0.10, 1.0)
        _TopColor           ("Top Color (bright)",   Color)              = (0.35, 0.65, 0.20, 1.0)
        _CreaseWidth        ("Crease Width",         Range(0.01, 0.12))  = 0.07
        _CreaseDarken       ("Crease Darken",        Range(0.3, 1.0))    = 0.50

        [Header(Dome Shading)]
        _HighlightColor     ("Highlight Color",      Color)              = (0.55, 0.80, 0.35, 0.45)
        _HighlightSize      ("Highlight Size",       Range(0.1, 0.7))    = 0.30
        _HighlightOffset    ("Highlight Offset",     Range(-0.5, 0.5))   = 0.15
        _EdgeShade          ("Edge Shade",           Range(0.5, 1.0))    = 0.68

        [Header(Leaf Vein)]
        _VeinWidth          ("Vein Width",           Range(0.01, 0.15))  = 0.06
        _VeinDarken         ("Vein Darken",          Range(0.5, 1.0))    = 0.72
        _LateralVeinCount   ("Lateral Vein Count",   Range(3, 12))       = 6
        _LateralVeinAngle   ("Lateral Vein Angle",   Range(0.3, 3.0))    = 1.2

        [Header(Subsurface Scattering)]
        _SSSAbsorption      ("SSS Absorption",       Range(0.5, 10))     = 3.0
        _SSSStrength        ("SSS Strength",         Range(0, 1))        = 0.25
        _SSSColor           ("SSS Color",            Color)              = (0.6, 0.8, 0.2, 1.0)
        _LightDir           ("Light Direction",      Vector)             = (0.3, 0.7, 0, 0)

        [Header(Self Shadow)]
        _LeafShadowStrength ("Shadow Strength",      Range(0, 0.6))      = 0.35
        _LeafShadowSoftness ("Shadow Softness",      Range(0.01, 0.15))  = 0.05
        _LeafShadowOffsetX  ("Shadow Offset X",      Range(-0.06, 0.06)) = 0.02
        _LeafShadowOffsetY  ("Shadow Offset Y",      Range(-0.06, 0.06)) = -0.03
        _ShadowSamples      ("Shadow Samples",       Range(1, 8))        = 4
        _ShadowJitterRadius ("Shadow Jitter Radius", Range(0.001, 0.03)) = 0.01

        [Header(Ambient Occlusion)]
        _AOMul              ("AO Strength",          Range(0, 1))        = 0.4

        [Header(Colour Variation)]
        _HueJitter          ("Hue Jitter (degrees)", Range(0, 30))       = 10.0
        _AgeColor           ("Age Color (outer)",    Color)              = (0.45, 0.55, 0.15, 1.0)
        _AgeStrength        ("Age Strength",         Range(0, 0.5))      = 0.15
        _BrowningColor      ("Browning Color",       Color)              = (0.40, 0.28, 0.12, 1.0)
        _EdgeBrowningWidth  ("Edge Browning Width",  Range(0.01, 0.5))   = 0.15

        [Header(Ground Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow", Float)      = 1
        _ShadowColor        ("Shadow Color",         Color)              = (0.04, 0.04, 0.08, 0.45)
        _ShadowOffsetX      ("Shadow Offset X",     Range(-0.15, 0.15)) = 0.03
        _ShadowOffsetY      ("Shadow Offset Y",     Range(-0.15, 0.15)) = -0.04
        _ShadowSoftness     ("Shadow Softness",      Range(0, 0.10))     = 0.04

        [Header(Vein Texture)]
        _VeinTex            ("Vein Texture (opt)",   2D)                 = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "TransparentCutout"
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
            #pragma target 3.5
            #pragma shader_feature _SHADOW_ON
            #include "UnityCG.cginc"
            #include "Assets/Shaders/BalloonParty/Grid/Editor/GielisSDF.cginc"

            #define MAX_SLOTS      16
            #define LEAF_COUNT     16
            #define GOLDEN_ANGLE    2.39996323
            #define PHYLLO_MAX_R   3.93700394

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
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            fixed4 _RendererColor;

            float  _SlotRadius;
            float  _RadiusJitter;
            float  _AAWidth;

            float  _BranchSpread;
            float  _SubCircleSize;
            float  _SubCircleSizeVar;

            float  _GielisM;
            float  _GielisN1;
            float  _GielisN2;
            float  _GielisN3;

            fixed4 _BaseColor;
            fixed4 _TopColor;
            float  _CreaseWidth;
            float  _CreaseDarken;

            fixed4 _HighlightColor;
            float  _HighlightSize;
            float  _HighlightOffset;
            float  _EdgeShade;

            float  _LateralVeinCount;
            float  _LateralVeinAngle;
            float  _VeinWidth;
            float  _VeinDarken;

            float  _SSSAbsorption;
            float  _SSSStrength;
            fixed4 _SSSColor;
            float4 _LightDir;

            float  _LeafShadowStrength;
            float  _LeafShadowSoftness;
            float  _LeafShadowOffsetX;
            float  _LeafShadowOffsetY;
            int    _ShadowSamples;
            float  _ShadowJitterRadius;

            float  _AOMul;

            float  _HueJitter;
            fixed4 _AgeColor;
            float  _AgeStrength;
            fixed4 _BrowningColor;
            float  _EdgeBrowningWidth;

            float4 _SlotCentersWorld[MAX_SLOTS];
            int    _SlotCount;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowOffsetX;
            float  _ShadowOffsetY;
            float  _ShadowSoftness;
            #endif

            // ── Phyllotaxis leaf generator — mirrors BushView.PhyllotaxisCenter ──
            void PhyllotaxisLeaf(float2 slotCenter, float baseRadius, float hash,
                                 int depth,
                                 out float2 center, out float radius,
                                 out float2 leafDir)
            {
                int n = LEAF_COUNT - 1 - depth;
                float fn = (float)n;

                float angle = fn * GOLDEN_ANGLE + hash * 6.283185;
                leafDir = float2(cos(angle), sin(angle));

                float dist = baseRadius * _BranchSpread * sqrt(fn + 0.5) / PHYLLO_MAX_R;

                float depthT = (float)depth / (float)(LEAF_COUNT - 1);
                float sizeBase = lerp(1.0, 0.65, depthT);
                float sizeHash = 0.85 + frac(hash * (fn + 1.0) * 23.1) * _SubCircleSizeVar;

                radius = baseRadius * _SubCircleSize * sizeBase * sizeHash;

                // Estimate half-length using average Gielis boundary at 0°
                float halfLen = radius * GielisRadius(0.0, _GielisM, _GielisN1, _GielisN2, _GielisN3);
                center = slotCenter + leafDir * (dist + halfLen);
            }

            // ── Leaf centre distance for dome shading ──
            float LeafCenterDist(float2 wp, float2 center, float radius)
            {
                return length(wp - center) / max(radius, 0.001);
            }

            // ── Canopy SDF for ground shadow — full-depth Gielis ──
            float BakeCanopySDF(float2 wp)
            {
                float d = 999.0;

                for (int i = 0; i < _SlotCount; i++)
                {
                    float2 sc = _SlotCentersWorld[i].xy;
                    float rs = _SlotCentersWorld[i].w > 0.001
                             ? _SlotCentersWorld[i].w : 1.0;
                    float h = frac(sin(dot(sc, float2(127.1, 311.7))) * 43758.5453);
                    float br = (_SlotRadius * rs) + (h - 0.5) * 2.0 * _RadiusJitter;

                    bool isGap = rs < 0.99;

                    for (int depth = 0; depth < LEAF_COUNT; depth++)
                    {
                        if (depth > 0 && isGap) continue;

                        float2 cc, ld;
                        float  cr;

                        if (isGap)
                        {
                            cc = sc;
                            cr = br * _SubCircleSize;
                            ld = float2(0, 1);
                        }
                        else
                        {
                            PhyllotaxisLeaf(sc, br, h, depth, cc, cr, ld);
                        }

                        float leafHash = frac(h * (float)(depth + 1) * 7.13);
                        float mL, n1L, n2L, n3L;
                        JitterGielisParams(leafHash, _GielisM, _GielisN1, _GielisN2, _GielisN3,
                                           mL, n1L, n2L, n3L);

                        float pt = isGap ? 0.0 : 1.0;
                        d = min(d, GielisSDF(wp, cc, cr * pt + cr * (1.0 - pt), ld,
                                             isGap ? 0.0 : mL,
                                             isGap ? 1.0 : n1L,
                                             isGap ? 1.0 : n2L,
                                             isGap ? 1.0 : n3L));
                    }
                }
                return d;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;

                // ── Per-slot precomputation ──
                float slotHash[MAX_SLOTS];
                float slotBaseR[MAX_SLOTS];
                float slotReachSq[MAX_SLOTS];

                float maxSubR = _SubCircleSize * (0.85 + _SubCircleSizeVar);
                float reachFactor = _BranchSpread + maxSubR * 3.0;

                for (int pi = 0; pi < _SlotCount; pi++)
                {
                    float2 sc = _SlotCentersWorld[pi].xy;
                    float rs = _SlotCentersWorld[pi].w > 0.001
                             ? _SlotCentersWorld[pi].w : 1.0;
                    slotHash[pi] = frac(sin(dot(sc, float2(127.1, 311.7))) * 43758.5453);
                    slotBaseR[pi] = (_SlotRadius * rs)
                        + (slotHash[pi] - 0.5) * 2.0 * _RadiusJitter;
                    float reach = slotBaseR[pi] * reachFactor + _AAWidth;
                    slotReachSq[pi] = reach * reach;
                }

                // ── Painter's algorithm — back to front ──
                bool   anyCovered = false;
                fixed3 leafColor  = fixed3(0, 0, 0);
                float  leafAlphaDist = 999.0;
                bool   wasLeafCovered = false;

                // Track the covering leaf's depth for AO
                int    coveringDepth = -1;
                int    coveringSlot  = -1;

                for (int depth = 0; depth < LEAF_COUNT; depth++)
                {
                    for (int si = 0; si < _SlotCount; si++)
                    {
                        float2 slotCenter = _SlotCentersWorld[si].xy;
                        bool isGapFill = _SlotCentersWorld[si].w > 0.001
                                      && _SlotCentersWorld[si].w < 0.99;

                        if (depth > 0 && isGapFill) continue;

                        float2 diff = wp - slotCenter;
                        if (dot(diff, diff) > slotReachSq[si]) continue;

                        float hash = slotHash[si];
                        float baseRadius = slotBaseR[si];

                        float2 cc, leafDir;
                        float  cr;

                        if (isGapFill)
                        {
                            cc = slotCenter;
                            cr = baseRadius * _SubCircleSize;
                            leafDir = float2(0, 1);
                        }
                        else
                        {
                            PhyllotaxisLeaf(slotCenter, baseRadius, hash, depth,
                                            cc, cr, leafDir);
                        }

                        // Per-leaf Gielis jitter
                        float leafHash = frac(hash * (float)(depth + 1) * 7.13);
                        float mL, n1L, n2L, n3L;
                        if (isGapFill)
                        {
                            mL = 0.0; n1L = 1.0; n2L = 1.0; n3L = 1.0;
                        }
                        else
                        {
                            JitterGielisParams(leafHash, _GielisM, _GielisN1, _GielisN2, _GielisN3,
                                               mL, n1L, n2L, n3L);
                        }

                        float d = GielisSDF(wp, cc, cr, leafDir, mL, n1L, n2L, n3L);
                        leafAlphaDist = min(leafAlphaDist, d);

                        if (d < 0.0)
                        {
                            float depthT = (float)depth / (float)(LEAF_COUNT - 1);
                            fixed3 circleColor = lerp(_BaseColor.rgb, _TopColor.rgb, depthT);

                            // ── Per-leaf dome shading ──
                            float edgeT = LeafCenterDist(wp, cc, cr);
                            float radial = smoothstep(1.0, 0.3, edgeT);
                            circleColor *= lerp(_EdgeShade, 1.0, radial);

                            float2 localP = wp - cc;

                            if (!isGapFill)
                            {
                                // Leaf-local frame
                                float2 tang = float2(-leafDir.y, leafDir.x);
                                float halfLen = cr * GielisRadius(0.0, mL, n1L, n2L, n3L);
                                float stemAxisT = saturate(
                                    (dot(localP, leafDir) + halfLen)
                                    / max(2.0 * halfLen, 0.001));

                                // ── SSS — Beer-Lambert transmittance ──
                                float thickness = saturate(-d / (cr * 0.5));
                                float transmittance = exp(-thickness * _SSSAbsorption);
                                float backLight = max(0, dot(-leafDir, _LightDir.xy));
                                circleColor = lerp(circleColor, _SSSColor.rgb,
                                    transmittance * _SSSStrength * (0.3 + backLight * 0.7));

                                // ── Highlight — fake specular ──
                                float axial = dot(localP, leafDir);
                                float perp  = dot(localP, tang);
                                float hlDist = length(float2(
                                    (axial - _HighlightOffset * halfLen) / max(halfLen, 0.001),
                                    perp / max(cr, 0.001)));
                                float hlT = smoothstep(_HighlightSize, 0.0, hlDist);
                                circleColor = lerp(circleColor, _HighlightColor.rgb,
                                    hlT * _HighlightColor.a);

                                // ── Midrib ──
                                float perpNorm = perp / max(cr, 0.001);
                                float vDist = abs(perpNorm);
                                float taperW = _VeinWidth * lerp(1.5, 0.3, stemAxisT);
                                float vLine = 1.0 - smoothstep(taperW * 0.4, taperW, vDist);
                                float vMask = smoothstep(0.02, 0.15, stemAxisT)
                                            * smoothstep(0.98, 0.7, stemAxisT);
                                circleColor *= lerp(1.0, _VeinDarken, vLine * vMask);

                                // ── Lateral veins ──
                                float veinField = stemAxisT * _LateralVeinCount
                                    - abs(perpNorm) * _LateralVeinAngle;
                                float veinFrac = frac(veinField);
                                float veinD = min(veinFrac, 1.0 - veinFrac);
                                float lateralW = _VeinWidth * lerp(1.0, 0.2, abs(perpNorm));
                                float latLine = 1.0 - smoothstep(lateralW * 0.3, lateralW, veinD);
                                float latMask = smoothstep(0.05, 0.2, stemAxisT)
                                    * smoothstep(0.98, 0.75, stemAxisT)
                                    * smoothstep(0.02, 0.12, abs(perpNorm));
                                circleColor *= lerp(1.0, _VeinDarken, latLine * latMask);

                                // ── Full-depth self-shadow with multi-sample penumbra ──
                                float selfSh = 0.0;
                                float2 shOffset = float2(_LeafShadowOffsetX, _LeafShadowOffsetY);
                                int samples = clamp(_ShadowSamples, 1, 8);

                                for (int sd = depth + 1; sd < LEAF_COUNT; sd++)
                                {
                                    float2 ucc, udir;
                                    float  ucr;
                                    PhyllotaxisLeaf(slotCenter, baseRadius, hash, sd,
                                                    ucc, ucr, udir);

                                    float uLeafHash = frac(hash * (float)(sd + 1) * 7.13);
                                    float uM, uN1, uN2, uN3;
                                    JitterGielisParams(uLeafHash, _GielisM, _GielisN1,
                                                       _GielisN2, _GielisN3,
                                                       uM, uN1, uN2, uN3);

                                    float sampleAcc = 0.0;
                                    for (int sj = 0; sj < samples; sj++)
                                    {
                                        float2 jPos = wp + shOffset
                                            + ShadowJitter[sj] * _ShadowJitterRadius;
                                        float usd = GielisSDF(jPos, ucc, ucr, udir,
                                                              uM, uN1, uN2, uN3);
                                        float st = 1.0 - smoothstep(
                                            -_LeafShadowSoftness,
                                             _LeafShadowSoftness, usd);
                                        sampleAcc += st;
                                    }
                                    sampleAcc /= (float)samples;
                                    selfSh = max(selfSh, sampleAcc);
                                }
                                circleColor *= 1.0 - selfSh * _LeafShadowStrength;

                                // ── Hue jitter ──
                                float hueShift = (leafHash - 0.5) * 2.0 * _HueJitter / 360.0;
                                circleColor = HueRotate(circleColor, hueShift);

                                // ── Age gradient — outer leaves yellower ──
                                circleColor = lerp(circleColor, _AgeColor.rgb,
                                    depthT * _AgeStrength);

                                // ── Edge browning ──
                                float edgeBrown = smoothstep(0.0, _EdgeBrowningWidth,
                                    -d / max(cr, 0.001));
                                circleColor = lerp(_BrowningColor.rgb, circleColor, edgeBrown);
                            }
                            else
                            {
                                float hlT = smoothstep(_HighlightSize, 0.0, edgeT);
                                circleColor = lerp(circleColor, _HighlightColor.rgb,
                                    hlT * _HighlightColor.a);
                            }

                            // ── Crease at overlaps ──
                            if (wasLeafCovered)
                            {
                                float crease = smoothstep(0.0, _CreaseWidth, -d);
                                circleColor *= lerp(_CreaseDarken, 1.0, crease);
                            }

                            leafColor = circleColor;
                            anyCovered = true;
                            wasLeafCovered = true;
                            coveringDepth = depth;
                            coveringSlot = si;
                        }
                    }
                }

                // ── Ambient occlusion — count all overlapping leaves above ──
                if (anyCovered && coveringDepth >= 0)
                {
                    float aoCount = 0;
                    float aoTotal = 0;

                    for (int aoi = 0; aoi < _SlotCount; aoi++)
                    {
                        float2 sc = _SlotCentersWorld[aoi].xy;
                        bool isGap = _SlotCentersWorld[aoi].w > 0.001
                                   && _SlotCentersWorld[aoi].w < 0.99;
                        float h = slotHash[aoi];
                        float br = slotBaseR[aoi];

                        int startD = (aoi == coveringSlot) ? coveringDepth + 1 : 0;

                        for (int aod = startD; aod < LEAF_COUNT; aod++)
                        {
                            if (aod > 0 && isGap) continue;

                            float2 acc, ald;
                            float  acr;

                            if (isGap)
                            {
                                acc = sc;
                                acr = br * _SubCircleSize;
                                ald = float2(0, 1);
                            }
                            else
                            {
                                PhyllotaxisLeaf(sc, br, h, aod, acc, acr, ald);
                            }

                            float aLeafHash = frac(h * (float)(aod + 1) * 7.13);
                            float aM, aN1, aN2, aN3;
                            if (isGap)
                            {
                                aM = 0.0; aN1 = 1.0; aN2 = 1.0; aN3 = 1.0;
                            }
                            else
                            {
                                JitterGielisParams(aLeafHash, _GielisM, _GielisN1,
                                                   _GielisN2, _GielisN3,
                                                   aM, aN1, aN2, aN3);
                            }

                            float aoD = GielisSDF(wp, acc, acr, ald, aM, aN1, aN2, aN3);
                            if (aoD < 0.0)
                            {
                                aoCount += 1.0;
                            }
                            aoTotal += 1.0;
                        }
                    }

                    float ao = 1.0 - (aoCount / max(aoTotal, 1.0)) * _AOMul;
                    leafColor *= ao;
                }

                float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, leafAlphaDist);

                // ── Ground shadow — full Gielis ──
                #ifdef _SHADOW_ON
                float2 shadowWp = wp - float2(_ShadowOffsetX, _ShadowOffsetY);

                // Multi-sample soft shadow
                float shadowAcc = 0.0;
                int shSamples = clamp(_ShadowSamples, 1, 8);
                for (int shS = 0; shS < shSamples; shS++)
                {
                    float2 sWp = shadowWp + ShadowJitter[shS] * _ShadowSoftness;
                    float shadowD = BakeCanopySDF(sWp);
                    shadowAcc += 1.0 - smoothstep(-_ShadowSoftness, 0.0, shadowD);
                }
                float shadowAlpha = (shadowAcc / (float)shSamples)
                                  * _ShadowColor.a * IN.color.a;

                if (alpha < 0.001 && shadowAlpha < 0.001) discard;

                if (alpha < 0.001)
                {
                    return fixed4(_ShadowColor.rgb * shadowAlpha, shadowAlpha);
                }
                #else
                if (alpha < 0.001) discard;
                #endif

                // ── Final composition — premultiplied alpha ──
                float mainAlpha = alpha * IN.color.a;
                fixed3 mainRgb = leafColor * IN.color.rgb;

                #ifdef _SHADOW_ON
                float combinedA = mainAlpha + shadowAlpha * (1.0 - mainAlpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (mainRgb * mainAlpha
                       + _ShadowColor.rgb * shadowAlpha * (1.0 - mainAlpha))
                      / combinedA
                    : mainRgb;
                return fixed4(combinedRGB * combinedA, combinedA);
                #else
                return fixed4(mainRgb * mainAlpha, mainAlpha);
                #endif
            }
            ENDCG
        }
    }
}

