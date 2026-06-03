Shader "BalloonParty/Grid/Bush"
{
    // Procedural top-down cartoony bush on a SpriteRenderer quad.
    // Each slot spawns overlapping leaf-clump circles arranged in a branch
    // pattern. Circles are painted back-to-front (painter's algorithm) so
    // upper circles fully occlude lower ones — no venn-diagram blending.
    // Inner shadow creases darken where an upper circle overlaps a lower one.
    // Brown branch capsules show through gaps in the leaf canopy.
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
        _BranchSpread       ("Branch Spread",        Range(0.1, 0.8))    = 0.45
        _SubCircleSize      ("Circle Size",          Range(0.2, 0.9))    = 0.55
        _SubCircleSizeVar   ("Size Variation",       Range(0, 0.5))      = 0.30

        [Header(Surface)]
        _BaseColor          ("Base Color (deep)",    Color)              = (0.14, 0.40, 0.10, 1.0)
        _TopColor           ("Top Color (bright)",   Color)              = (0.35, 0.65, 0.20, 1.0)
        _CreaseWidth        ("Crease Width",         Range(0.01, 0.12))  = 0.05
        _CreaseDarken       ("Crease Darken",        Range(0.3, 1.0))    = 0.65

        [Header(Dome Shading)]
        _HighlightColor     ("Highlight Color",      Color)              = (0.55, 0.80, 0.35, 0.45)
        _HighlightSize      ("Highlight Size",       Range(0.1, 0.7))    = 0.35
        _EdgeShade          ("Edge Shade",           Range(0.5, 1.0))    = 0.75

        [Header(Branches)]
        _BranchThickness    ("Branch Thickness",     Range(0.005, 0.05)) = 0.018
        _BranchColor        ("Branch Color",         Color)              = (0.35, 0.22, 0.10, 1.0)

        [Header(Scallop)]
        _ScallopDepth       ("Scallop Depth",        Range(0, 0.04))     = 0.015
        _ScallopSize        ("Scallop Size",         Range(0.005, 0.06)) = 0.025

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

            #define MAX_SLOTS     16
            #define SUB_CIRCLES    5
            #define SCALLOP_COUNT  5

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

            fixed4 _BaseColor;
            fixed4 _TopColor;
            float  _CreaseWidth;
            float  _CreaseDarken;

            fixed4 _HighlightColor;
            float  _HighlightSize;
            float  _EdgeShade;

            float  _BranchThickness;
            fixed4 _BranchColor;

            float  _ScallopDepth;
            float  _ScallopSize;

            float  _WindSpeed;
            float  _WindAmount;
            float  _TimeOffset;

            float4 _SlotCentersWorld[MAX_SLOTS];
            int    _SlotCount;

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

            // ── Sub-circle generator ──
            // idx 0 = centre, 1-4 = branches at ~90° with hash variation.
            void SubCircle(float2 slotCenter, float baseRadius, float hash, int idx,
                           out float2 center, out float radius)
            {
                if (idx == 0)
                {
                    center = slotCenter;
                    radius = baseRadius * _SubCircleSize;
                    return;
                }

                float fi = (float)idx;
                float angle = hash * 6.283185 + (fi - 1.0) * 1.5708
                            + (frac(hash * fi * 17.3) - 0.5) * 0.7;
                float spread = baseRadius * _BranchSpread
                             * (0.8 + frac(hash * fi * 13.7) * 0.4);
                float sizeVar = 1.0 - frac(hash * fi * 23.1) * _SubCircleSizeVar;

                center = slotCenter + float2(cos(angle), sin(angle)) * spread;
                radius = baseRadius * _SubCircleSize * sizeVar;
            }

            // ── Scalloped circle SDF ──
            // Subtracts small notch circles around the rim for bumpy leaf edges.
            float ScallopedCircleSDF(float2 wp, float2 center, float radius, float hash)
            {
                float d = length(wp - center) - radius;

                for (int s = 0; s < SCALLOP_COUNT; s++)
                {
                    float fs = (float)s;
                    float angle = hash * 6.283185 + fs * (6.283185 / (float)SCALLOP_COUNT)
                                + frac(hash * fs * 31.7) * 0.6;
                    float2 notchPos = center + float2(cos(angle), sin(angle))
                                    * (radius - _ScallopDepth * 0.3);
                    float notchDist = length(wp - notchPos) - _ScallopSize;
                    // Subtract notch: where notchDist < 0, push d positive (outside)
                    d = max(d, -notchDist);
                }

                return d;
            }

            // ── Branch capsule SDF (line segment with thickness) ──
            float CapsuleSDF(float2 wp, float2 a, float2 b, float thickness)
            {
                float2 pa = wp - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h) - thickness;
            }

            // ── Simplified base SDF (slot centres only) for shadow ──
            float BaseSDF(float2 wp)
            {
                float d = 999.0;
                for (int i = 0; i < _SlotCount; i++)
                {
                    float2 c = _SlotCentersWorld[i].xy;
                    float rs = _SlotCentersWorld[i].w > 0.001
                             ? _SlotCentersWorld[i].w : 1.0;
                    float h = frac(sin(dot(c, float2(127.1, 311.7))) * 43758.5453);
                    float r = (_SlotRadius * rs) + (h - 0.5) * 2.0 * _RadiusJitter;
                    d = min(d, length(wp - c) - r);
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
                float  t  = _TimeOffset;

                // ── Wind + disturbance ──
                float2 wpEval = wp + WindDisplace(wp, t);

                #ifdef _DISTURBANCE_ON
                float2 fieldUV  = (wp - _FieldBoundsMin) / _FieldBoundsSize;
                float2 displace = (tex2D(_DisturbanceTex, fieldUV).gb - 0.5)
                                * 2.0 * _DisplaceWorldScale;
                wpEval += displace;
                #endif

                // ── Pass 1: branch skeleton (behind leaves) ──
                float branchDist = 999.0;
                for (int bi = 0; bi < _SlotCount; bi++)
                {
                    float radiusScale = _SlotCentersWorld[bi].w > 0.001
                                      ? _SlotCentersWorld[bi].w : 1.0;
                    // Skip gap fills for branches
                    if (radiusScale < 0.99) continue;

                    float2 slotCenter = _SlotCentersWorld[bi].xy;
                    float hash = frac(sin(dot(slotCenter, float2(127.1, 311.7)))
                               * 43758.5453);
                    float baseRadius = (_SlotRadius * radiusScale)
                                     + (hash - 0.5) * 2.0 * _RadiusJitter;

                    // 3 branch capsules matching sub-circle directions
                    for (int bc = 1; bc < SUB_CIRCLES; bc++)
                    {
                        float2 tipCenter;
                        float  tipRadius;
                        SubCircle(slotCenter, baseRadius, hash, bc, tipCenter, tipRadius);
                        float bd = CapsuleSDF(wpEval, slotCenter, tipCenter, _BranchThickness);
                        branchDist = min(branchDist, bd);
                    }
                }
                bool insideBranch = branchDist < 0.0;

                // ── Pass 2: leaf circles — painter's algorithm (back to front) ──
                // Depth order: centre circle (idx 0) is deepest, branch circles
                // (idx 1-3) sit on top. Within each slot, paint back to front.
                // Across slots, all circles at the same depth index are painted
                // together — gives consistent layering.
                bool  anyCovered = false;
                fixed3 leafColor = fixed3(0, 0, 0);
                float leafAlphaDist = 999.0;
                bool  wasLeafCovered = false;

                // Paint depth layers 0 (deepest) through SUB_CIRCLES-1 (topmost)
                for (int depth = 0; depth < SUB_CIRCLES; depth++)
                {
                    for (int si = 0; si < _SlotCount; si++)
                    {
                        float2 slotCenter = _SlotCentersWorld[si].xy;
                        float radiusScale = _SlotCentersWorld[si].w > 0.001
                                          ? _SlotCentersWorld[si].w : 1.0;

                        // Gap fills only have depth 0 (centre circle)
                        if (depth > 0 && radiusScale < 0.99) continue;

                        float hash = frac(sin(dot(slotCenter, float2(127.1, 311.7)))
                                   * 43758.5453);
                        float baseRadius = (_SlotRadius * radiusScale)
                                         + (hash - 0.5) * 2.0 * _RadiusJitter;

                        float2 cc;
                        float  cr;
                        SubCircle(slotCenter, baseRadius, hash, depth, cc, cr);

                        float d = ScallopedCircleSDF(wpEval, cc, cr, hash + depth * 0.37);
                        leafAlphaDist = min(leafAlphaDist, d);

                        if (d < 0.0)
                        {
                            // Depth-based colour: deeper = darker, top = brighter
                            float depthT = (float)depth / (float)(SUB_CIRCLES - 1);
                            fixed3 circleColor = lerp(_BaseColor.rgb, _TopColor.rgb, depthT);

                            // Per-circle dome shading: radial gradient from bright
                            // center to dark edge sells each clump as a 3D dome
                            float rawDist = length(wpEval - cc);
                            float edgeT = saturate(rawDist / max(cr, 0.001));
                            float radial = smoothstep(1.0, 0.3, edgeT);
                            circleColor *= lerp(_EdgeShade, 1.0, radial);

                            // Highlight spot near center
                            float hlT = smoothstep(_HighlightSize, 0.0, edgeT);
                            circleColor = lerp(circleColor, _HighlightColor.rgb,
                                               hlT * _HighlightColor.a);

                            // Inner shadow crease: darken near the edge of this
                            // circle where it overlaps an already-covered area
                            if (wasLeafCovered)
                            {
                                float crease = smoothstep(0.0, _CreaseWidth, -d);
                                circleColor *= lerp(_CreaseDarken, 1.0, crease);
                            }

                            // Painter's overwrite — upper circles fully cover lower
                            leafColor = circleColor;
                            anyCovered = true;
                            wasLeafCovered = true;
                        }
                    }
                }

                float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, leafAlphaDist);

                // If no leaf covers this pixel, check branch
                if (!anyCovered && insideBranch)
                {
                    leafColor = _BranchColor.rgb;
                    alpha = 1.0 - smoothstep(-_AAWidth, 0.0, branchDist);
                    anyCovered = true;
                }

                // ── Shadow ──
                #ifdef _SHADOW_ON
                float2 shadowWp = wp - float2(_ShadowOffsetX, _ShadowOffsetY);
                float shadowD = BaseSDF(shadowWp);
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
