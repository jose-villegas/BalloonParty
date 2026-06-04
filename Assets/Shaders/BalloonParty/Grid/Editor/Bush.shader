Shader "BalloonParty/Grid/Bush"
{
    // Procedural top-down cartoony bush on a SpriteRenderer quad.
    // Leaf clumps are placed in a Douady-Couder phyllotaxis spiral (golden
    // angle ≈ 137.5°) per slot. Each leaf is a circle-cut bilateral lens
    // (simplified Gielis via CSG) pointing radially outward. Painted
    // back-to-front (painter's algorithm) — outer/lower leaves first,
    // inner/top leaves last. Inner shadow creases, self-shadow, per-leaf
    // dome shading, central vein, and optional branches.
    //
    // GPU instancing DISABLED — _TimeOffset driven via MaterialPropertyBlock.
    //
    // _DisturbanceTex, _FieldBoundsMin, _FieldBoundsSize are GLOBAL shader
    // properties set by DisturbanceFieldService.
    //
    // MPB contract: _SlotCentersWorld, _SlotCount, _TimeOffset — set by ClusterView.
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
        _LeafPointiness     ("Leaf Pointiness",      Range(0.0, 2.0))    = 0.7

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

        [Header(Branches)]
        _BranchThickness    ("Branch Thickness",     Range(0.005, 0.05)) = 0.014
        _BranchColor        ("Branch Color",         Color)              = (0.35, 0.22, 0.10, 1.0)

        [Header(Self Shadow)]
        _LeafShadowStrength ("Shadow Strength",      Range(0, 0.6))      = 0.35
        _LeafShadowSoftness ("Shadow Softness",      Range(0.01, 0.15))  = 0.05
        _LeafShadowOffsetX  ("Shadow Offset X",      Range(-0.06, 0.06)) = 0.02
        _LeafShadowOffsetY  ("Shadow Offset Y",      Range(-0.06, 0.06)) = -0.03

        [Header(Wind)]
        _WindSpeed          ("Wind Speed",           Range(0, 2))        = 0.4
        _WindAmount         ("Wind Amount",          Range(0, 0.05))     = 0.015

        [Header(Animation)]
        _TimeOffset         ("Time Offset",          Float)              = 0.0

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow", Float)      = 1
        _ShadowColor        ("Shadow Color",         Color)              = (0.04, 0.04, 0.08, 0.45)
        _ShadowOffsetX      ("Shadow Offset X",     Range(-0.15, 0.15)) = 0.03
        _ShadowOffsetY      ("Shadow Offset Y",     Range(-0.15, 0.15)) = -0.04
        _ShadowSoftness     ("Shadow Softness",      Range(0, 0.10))     = 0.04

        [Header(Disturbance)]
        [Toggle(_DISTURBANCE_ON)] _EnableDisturbance ("Enable Disturbance", Float) = 0
        _DisplaceWorldScale  ("Displace World Scale",  Range(0, 2))      = 0.3
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature _SHADOW_ON
            #pragma shader_feature _DISTURBANCE_ON
            #include "UnityCG.cginc"
            #include "Assets/Shaders/BalloonParty/Noise/SimplexNoise2D.cginc"

            #define MAX_SLOTS      16
            #define LEAF_COUNT     16
            #define BRANCH_COUNT    5
            #define GOLDEN_ANGLE    2.39996323
            #define MAX_BRANCHES   (MAX_SLOTS * BRANCH_COUNT)
            #define SHADOW_DEPTH_LAYERS  4
            #define SELF_SHADOW_LAYERS   4
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

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            // ── Uniforms ──
            float  _SlotRadius;
            float  _RadiusJitter;
            float  _AAWidth;

            float  _BranchSpread;
            float  _SubCircleSize;
            float  _SubCircleSizeVar;
            float  _LeafPointiness;

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

            float  _BranchThickness;
            fixed4 _BranchColor;

            float  _LeafShadowStrength;
            float  _LeafShadowSoftness;
            float  _LeafShadowOffsetX;
            float  _LeafShadowOffsetY;

            float  _WindSpeed;
            float  _WindAmount;
            float  _TimeOffset;

            float4 _SlotCentersWorld[MAX_SLOTS];
            int    _SlotCount;

            float4 _BranchSegments[MAX_BRANCHES];
            int    _BranchCount;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowOffsetX;
            float  _ShadowOffsetY;
            float  _ShadowSoftness;
            #endif

            #ifdef _DISTURBANCE_ON
            sampler2D _DisturbanceTex;
            float2    _FieldBoundsMin;
            float2    _FieldBoundsSize;
            float     _DisplaceWorldScale;
            #endif

            // ── Phyllotaxis leaf generator (Douady-Couder golden-angle spiral) ──
            // depth 0 = outermost leaf (painted first / underneath)
            // depth LEAF_COUNT-1 = centre leaf (painted last / on top)
            void PhyllotaxisLeaf(float2 slotCenter, float baseRadius, float hash,
                                  int depth,
                                  out float2 center, out float radius,
                                  out float2 leafDir)
            {
                // Reverse: depth 0 → highest phyllotaxis index (outermost)
                int n = LEAF_COUNT - 1 - depth;
                float fn = (float)n;

                // Golden-angle spiral placement
                float angle = fn * GOLDEN_ANGLE + hash * 6.283185;
                leafDir = float2(cos(angle), sin(angle));

                // Douady-Couder distance: ∝ √n, normalized to slot radius
                float dist = baseRadius * _BranchSpread * sqrt(fn + 0.5) / PHYLLO_MAX_R;

                // Outer leaves (low depth) larger, inner leaves (high depth) smaller
                float depthT = (float)depth / (float)(LEAF_COUNT - 1);
                float sizeBase = lerp(1.0, 0.65, depthT);
                float sizeHash = 0.85 + frac(hash * (fn + 1.0) * 23.1) * _SubCircleSizeVar;

                radius = baseRadius * _SubCircleSize * sizeBase * sizeHash;

                // Offset so the leaf BASE sits at the spiral position
                // (branch tip) and the leaf grows outward
                float halfLen = radius * sqrt(1.0 + 2.0 * _LeafPointiness);
                center = slotCenter + leafDir * (dist + halfLen);
            }

            // ── Leaf-shaped SDF (circle-cut bilateral lens) ──
            // Two equal circles offset perpendicular to the leaf axis.
            // Their intersection (max of SDFs) forms a bilaterally
            // symmetric lens with pointed tips — a simplified Gielis
            // superformula realised via CSG circle cuts.
            //   pointiness 0 = circle, 0.7 = natural leaf, 2.0 = needle
            float LeafSDF(float2 wp, float2 center, float radius,
                          float2 leafDir, float pointiness)
            {
                float2 local = wp - center;
                float2 tang = float2(-leafDir.y, leafDir.x);
                float u = dot(local, leafDir);
                float v = dot(local, tang);

                float d = pointiness * radius;
                float R = radius + d;

                float d1 = length(float2(u, v - d)) - R;
                float d2 = length(float2(u, v + d)) - R;

                return max(d1, d2);
            }

            // ── Raw leaf distance (for dome shading — ignores pointiness) ──
            float LeafCenterDist(float2 wp, float2 center, float radius)
            {
                return length(wp - center) / max(radius, 0.001);
            }

            // ── Branch capsule SDF ──
            float CapsuleSDF(float2 wp, float2 a, float2 b, float thickness)
            {
                float2 pa = wp - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - thickness;
            }

            // ── Canopy silhouette SDF for ground shadow ──
            // Slot-outer loop: hash computed once per slot, distance
            // check skips far slots before any PhyllotaxisLeaf work
            float CanopySDF(float2 wp)
            {
                float d = 999.0;
                float maxSubR = _SubCircleSize * (0.85 + _SubCircleSizeVar);
                float ptSqrt = sqrt(1.0 + 2.0 * _LeafPointiness);
                float reachF = _BranchSpread
                             + maxSubR * (ptSqrt + 1.0 + _LeafPointiness);

                for (int i = 0; i < _SlotCount; i++)
                {
                    float2 sc = _SlotCentersWorld[i].xy;
                    float rs = _SlotCentersWorld[i].w > 0.001
                             ? _SlotCentersWorld[i].w : 1.0;
                    float h = frac(sin(dot(sc, float2(127.1, 311.7)))
                             * 43758.5453);
                    float br = (_SlotRadius * rs)
                             + (h - 0.5) * 2.0 * _RadiusJitter;

                    float reach = br * reachF;
                    float2 diff = wp - sc;
                    if (dot(diff, diff) > reach * reach) continue;

                    bool isGap = rs < 0.99;
                    for (int depth = 0; depth < SHADOW_DEPTH_LAYERS; depth++)
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

                        float pt = isGap ? 0.0 : _LeafPointiness;
                        d = min(d, LeafSDF(wp, cc, cr, ld, pt));
                    }
                }
                return d;
            }

            // ── Wind ──
            float2 WindDisplace(float2 wp, float t)
            {
                float2 windP = wp * 0.5 + float2(t * _WindSpeed, t * _WindSpeed * 0.7);
                float windN = SimplexNoise2D(windP);
                return float2(windN, windN * 0.6) * _WindAmount;
            }

            // ── Vertex shader ──
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            // ── Fragment shader ──
            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;

                // ── Early bounds check (squared distance — avoids sqrt) ──
                float maxReach = _SlotRadius * (1.0 + _BranchSpread)
                               * (1.0 + _LeafPointiness) + 0.2;
                float maxReachSq = maxReach * maxReach;
                float closestSlotSq = 999.0;
                for (int ei = 0; ei < _SlotCount; ei++)
                {
                    float2 dv = wp - _SlotCentersWorld[ei].xy;
                    closestSlotSq = min(closestSlotSq, dot(dv, dv));
                }
                if (closestSlotSq > maxReachSq) discard;

                float  t  = _TimeOffset;

                // ── Wind + disturbance ──
                float2 wpEval = wp + WindDisplace(wp, t);

                #ifdef _DISTURBANCE_ON
                float2 fieldUV  = (wp - _FieldBoundsMin) / _FieldBoundsSize;
                float2 displace = (tex2D(_DisturbanceTex, fieldUV).gb - 0.5)
                                * 2.0 * _DisplaceWorldScale;
                wpEval += displace;
                #endif

                // ── Per-slot precomputation ──
                // Hash and base radius are invariant across depth
                // layers. Precomputing avoids redundant sin() calls
                // in the main loop. Squared reach enables per-slot
                // early skip that avoids PhyllotaxisLeaf + LeafSDF.
                float slotHash[MAX_SLOTS];
                float slotBaseR[MAX_SLOTS];
                float slotReachSq[MAX_SLOTS];

                float maxSubR = _SubCircleSize
                              * (0.85 + _SubCircleSizeVar);
                float ptSqrt = sqrt(1.0 + 2.0 * _LeafPointiness);
                float reachFactor = _BranchSpread
                    + maxSubR * (ptSqrt + 1.0 + _LeafPointiness);

                for (int pi = 0; pi < _SlotCount; pi++)
                {
                    float2 sc = _SlotCentersWorld[pi].xy;
                    float rs = _SlotCentersWorld[pi].w > 0.001
                             ? _SlotCentersWorld[pi].w : 1.0;
                    slotHash[pi] = frac(sin(dot(sc,
                        float2(127.1, 311.7))) * 43758.5453);
                    slotBaseR[pi] = (_SlotRadius * rs)
                        + (slotHash[pi] - 0.5) * 2.0 * _RadiusJitter;
                    float reach = slotBaseR[pi] * reachFactor
                                + _AAWidth;
                    slotReachSq[pi] = reach * reach;
                }

                // ── Leaf clumps — painter's algorithm (back to front) ──
                // depth 0 = outermost (painted first), LEAF_COUNT-1 = centre (last)
                bool   anyCovered = false;
                fixed3 leafColor  = fixed3(0, 0, 0);
                float  leafAlphaDist = 999.0;
                bool   wasLeafCovered = false;

                for (int depth = 0; depth < LEAF_COUNT; depth++)
                {
                    for (int si = 0; si < _SlotCount; si++)
                    {
                        float2 slotCenter = _SlotCentersWorld[si].xy;
                        bool isGapFill = _SlotCentersWorld[si].w > 0.001
                                      && _SlotCentersWorld[si].w < 0.99;

                        if (depth > 0 && isGapFill) continue;

                        // Per-slot spatial skip
                        float2 diff = wpEval - slotCenter;
                        if (dot(diff, diff) > slotReachSq[si]) continue;

                        float hash = slotHash[si];
                        float baseRadius = slotBaseR[si];

                        float2 cc, leafDir;
                        float  cr;

                        if (isGapFill)
                        {
                            // Gap fill: simple circle at slot centre
                            cc = slotCenter;
                            cr = baseRadius * _SubCircleSize;
                            leafDir = float2(0, 1);
                        }
                        else
                        {
                            PhyllotaxisLeaf(slotCenter, baseRadius, hash, depth,
                                            cc, cr, leafDir);
                        }

                        // Leaf SDF (lens for real slots, circle for gaps)
                        float pointiness = isGapFill
                                         ? 0.0 : _LeafPointiness;
                        float d = LeafSDF(wpEval, cc, cr, leafDir, pointiness);
                        leafAlphaDist = min(leafAlphaDist, d);

                        if (d < 0.0)
                        {
                            // Depth-based colour
                            float depthT = (float)depth / (float)(LEAF_COUNT - 1);
                            fixed3 circleColor = lerp(_BaseColor.rgb, _TopColor.rgb,
                                                      depthT);

                            // Per-leaf dome shading (circular — sells dome shape)
                            float edgeT = LeafCenterDist(wpEval, cc, cr);
                            float radial = smoothstep(1.0, 0.3, edgeT);
                            circleColor *= lerp(_EdgeShade, 1.0, radial);

                            float2 localP = wpEval - cc;

                            // Leaf-local frame (shared by highlight and veins)
                            float2 tang = float2(-leafDir.y, leafDir.x);
                            float halfLen = cr
                                * sqrt(1.0 + 2.0 * pointiness);
                            float stemAxisT = saturate(
                                (dot(localP, leafDir) + halfLen)
                                / max(2.0 * halfLen, 0.001));

                            // Highlight — fake specular positioned along
                            // leaf axis, elliptical to match leaf shape
                            if (!isGapFill)
                            {
                                float axial = dot(localP, leafDir);
                                float perp = dot(localP, tang);
                                float hlDist = length(float2(
                                    (axial - _HighlightOffset * halfLen)
                                        / max(halfLen, 0.001),
                                    perp / max(cr, 0.001)));
                                float hlT = smoothstep(
                                    _HighlightSize, 0.0, hlDist);
                                circleColor = lerp(circleColor,
                                    _HighlightColor.rgb,
                                    hlT * _HighlightColor.a);

                                // Midrib — central vein from stem to tip
                                float perpNorm = perp / max(cr, 0.001);
                                float vDist = abs(perpNorm);
                                float taperW = _VeinWidth
                                    * lerp(1.5, 0.3, stemAxisT);
                                float vLine = 1.0 - smoothstep(
                                    taperW * 0.4, taperW, vDist);
                                float vMask = smoothstep(0.02, 0.15, stemAxisT)
                                            * smoothstep(0.98, 0.7, stemAxisT);
                                circleColor *= lerp(1.0, _VeinDarken,
                                                    vLine * vMask);

                                // Lateral veins — pinnate fishbone pattern
                                float veinField = stemAxisT * _LateralVeinCount
                                    - abs(perpNorm) * _LateralVeinAngle;
                                float veinFrac = frac(veinField);
                                float veinD = min(veinFrac, 1.0 - veinFrac);
                                float lateralW = _VeinWidth
                                    * lerp(1.0, 0.2, abs(perpNorm));
                                float latLine = 1.0 - smoothstep(
                                    lateralW * 0.3, lateralW, veinD);
                                float latMask = smoothstep(0.05, 0.2, stemAxisT)
                                    * smoothstep(0.98, 0.75, stemAxisT)
                                    * smoothstep(0.02, 0.12, abs(perpNorm));
                                circleColor *= lerp(1.0, _VeinDarken,
                                                    latLine * latMask);

                                // Self-shadow — circle check (cheaper than
                                // full LeafSDF, sufficient for soft darkening)
                                float selfSh = 0.0;
                                float2 shPos = wpEval
                                    + float2(_LeafShadowOffsetX,
                                             _LeafShadowOffsetY);
                                int maxSd = min(depth + SELF_SHADOW_LAYERS,
                                                LEAF_COUNT);
                                for (int sd = depth + 1; sd < maxSd; sd++)
                                {
                                    float2 ucc, udir;
                                    float  ucr;
                                    PhyllotaxisLeaf(slotCenter, baseRadius,
                                                    hash, sd, ucc, ucr, udir);
                                    float usd = length(shPos - ucc) - ucr;
                                    float st = 1.0 - smoothstep(
                                        -_LeafShadowSoftness,
                                         _LeafShadowSoftness, usd);
                                    selfSh = max(selfSh, st);
                                }
                                circleColor *= 1.0 - selfSh
                                             * _LeafShadowStrength;
                            }
                            else
                            {
                                float hlT = smoothstep(
                                    _HighlightSize, 0.0, edgeT);
                                circleColor = lerp(circleColor,
                                    _HighlightColor.rgb,
                                    hlT * _HighlightColor.a);
                            }

                            // Inner shadow crease at overlaps
                            if (wasLeafCovered)
                            {
                                float crease = smoothstep(0.0, _CreaseWidth, -d);
                                circleColor *= lerp(_CreaseDarken, 1.0, crease);
                            }

                            leafColor = circleColor;
                            anyCovered = true;
                            wasLeafCovered = true;
                        }
                    }
                }

                float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, leafAlphaDist);

                // ── Branch skeleton (deferred — only evaluated when
                //    no leaf covers the pixel) ──
                if (!anyCovered)
                {
                    float branchDist = 999.0;
                    for (int bi = 0; bi < _BranchCount; bi++)
                    {
                        float4 seg = _BranchSegments[bi];
                        branchDist = min(branchDist,
                            CapsuleSDF(wpEval, seg.xy, seg.zw,
                                       _BranchThickness));
                    }
                    if (branchDist < 0.0)
                    {
                        leafColor = _BranchColor.rgb;
                        alpha = 1.0 - smoothstep(-_AAWidth, 0.0,
                                                  branchDist);
                        anyCovered = true;
                    }
                }

                // ── Shadow ──
                #ifdef _SHADOW_ON
                float2 shadowWp = wp - float2(_ShadowOffsetX, _ShadowOffsetY);
                float shadowD = CanopySDF(shadowWp);
                float shadowAlpha = (1.0 - smoothstep(-_ShadowSoftness, 0.0, shadowD))
                                  * _ShadowColor.a * IN.color.a;

                if (alpha < 0.001 && shadowAlpha < 0.001) discard;
                if (alpha < 0.001) return fixed4(_ShadowColor.rgb, shadowAlpha);
                #else
                if (alpha < 0.001) discard;
                #endif

                // ── Final composition ──
                fixed3 mainRgb   = leafColor * IN.color.rgb;
                float  mainAlpha = alpha * IN.color.a;

                #ifdef _SHADOW_ON
                fixed  combinedA   = mainAlpha + shadowAlpha * (1.0 - mainAlpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (mainRgb * mainAlpha
                       + _ShadowColor.rgb * shadowAlpha * (1.0 - mainAlpha))
                      / combinedA
                    : mainRgb;
                return fixed4(combinedRGB, combinedA);
                #else
                return fixed4(mainRgb, mainAlpha);
                #endif
            }
            ENDCG
        }
    }
}
