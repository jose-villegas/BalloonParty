Shader "BalloonParty/Scenario/Paper"
{
    // The page bonus (extra-colour) levels play on: GroundView's paper quad, over the floor and the blades, under the
    // smoke (drawn as ink there) and the clouds. A papyrus-like sheet from one baked tile (Tools/art/make_paper.py:
    // R tone, GB slope, A the sheet's smooth undulation), so no noise runs per frame. The scene light rakes its relief per pixel, by the
    // light field's direction where a pop or an item lights it, and tints it with the time of day: two taps of the
    // tile for the fibres and grain (the second turned and larger, so the repeat never shows) and three of its
    // undulation far larger for the sheet's slow swells. A fourth tap, of the floors' speckle map, flags a very sparse
    // share of grains that glint where their own little facet turns to the light.
    // Coverage: opaque at rest. From the sky it comes and goes through the mist reveal like the meadow
    // (GroundCoverageAt). From the meadow or the water it opens straight over them (_PaperReveal, a paper swap): it
    // shows inside the hole on the way in and outside it on the way out, with a frayed, slightly browned edge that
    // casts a soft shadow on the surface under it.
    // Ink: the pops' and the Paint item's blots (SmokeFieldService's ink field, _InkFieldTex) are drawn into the page
    // here as a watercolour glaze, under its relief and its light, so they soak into the paper rather than lie on it
    // and never hide the balloons' colours, and cover the screen wherever the page does.
    Properties
    {
        [NoScaleOffset] _PaperTex ("Paper (R tone, GB slope, A undulation, linear)", 2D) = "gray" {}
        _TileSize       ("Tile Size (world)",           Float)           = 3.0

        [Header(Colour)]
        _PaperColor     ("Paper Colour",                Color)           = (0.95, 0.93, 0.87, 1)
        _FibreColor     ("Fibre Colour",                Color)           = (0.74, 0.66, 0.52, 1)
        // How strongly the tile's tone (fibres, strips, specks) pulls toward the fibre colour.
        _ToneStrength   ("Fibre Tone",                  Range(0, 1))     = 0.8

        [Header(Relief)]
        // How much the fibres' and grain's bump shades toward the light.
        _Relief         ("Fibre Relief",                Range(0, 1))     = 0.35
        // The sheet's slow swells: their world size, and how much they shade toward the light.
        _UndulationSize ("Undulation Size (world)",     Float)           = 14.0
        _Undulation     ("Undulation Relief",           Range(0, 1))     = 0.25

        [Header(Glints)]
        // The floors' speckle map (Tools/art/make_detail_noise.py): GB = a grain's slope, A = a flattened speckle, so
        // a threshold of 1 - share flags exactly that share of grains.
        [NoScaleOffset] _GlintTex ("Grain Speckle (GB slope, A flattened)", 2D) = "gray" {}
        _GlintSize      ("Grain Tile Size (world)",     Float)           = 1.6
        // The share of grains that can glint: keep it tiny, a few specks across the screen.
        _GlintShare     ("Glint Share",                 Range(0, 0.05))  = 0.006
        _GlintStrength  ("Glint Strength",              Range(0, 2))     = 0.7

        [Header(Ink)]
        // The opacity a blot's edge sits at: higher pulls the edge in toward its core.
        _InkThreshold   ("Ink Edge Level",              Range(0.02, 0.9)) = 0.3
        // How far either side of the edge level the edge fades, in opacity: the blot bleeds softly into the paper.
        _InkBlur        ("Ink Blur",                    Range(0, 0.4))   = 0.18
        // The blot's border wanders: two taps of a tileable noise (R) push the edge about (world units) and lift or
        // drop its level (opacity), so an outline isn't its stamps' straight sides.
        [NoScaleOffset] _InkNoiseTex ("Ink Edge Noise (R)", 2D)          = "gray" {}
        _InkNoiseSize   ("Ink Noise Size (world)",      Float)           = 2.2
        _InkEdgeWarp    ("Ink Edge Wander",             Range(0, 0.3))   = 0.04
        _InkEdgeNoise   ("Ink Edge Raggedness",         Range(0, 0.6))   = 0.18
        // Watercolour: the ink glazes the page (multiplies it) rather than covering it, so the paper and whatever
        // sits over it keep their own colour. How much pigment a blot's body lays down: 1 = the colour itself.
        _InkWash        ("Wash Strength",               Range(0, 1))     = 0.4
        // Pigment gathered where the wash stopped spreading: extra strength in a line just inside the edge.
        _InkRim         ("Dried Edge",                  Range(0, 1))     = 0.3
        _InkRimWidth    ("Dried Edge Width",            Range(0.01, 0.5)) = 0.12
        // Pigment settling into the fibres' hollows (the tile's darker tone), so the wash is grainy, not flat.
        _InkGranulation ("Granulation",                 Range(0, 1))     = 0.5

        [Header(Lighting)]
        // Scene-light colour x intensity multiplies the paper: 0 = authored colours always, 1 = fully lit.
        _LightInfluence ("Light Influence",             Range(0, 1))     = 1

        [Header(Opening Edge)]
        // World units the edge frays along the fibres as the paper opens over the meadow or the water.
        _EdgeDeckle     ("Edge Fray",                   Range(0, 0.6))   = 0.22
        // The browned band just inside the paper's edge.
        _EdgeBrownColor ("Edge Colour",                 Color)           = (0.62, 0.50, 0.34, 1)
        _EdgeBrownWidth ("Edge Width (world)",          Range(0.01, 1))  = 0.18
        _EdgeBrown      ("Edge Strength",               Range(0, 1))     = 0.5
        // The paper's shadow on the surface under it: how far it falls from the edge, away from the light.
        _EdgeShadow     ("Edge Shadow",                 Range(0, 1))     = 0.35
        _EdgeShadowOffset ("Edge Shadow Offset (world)", Range(0, 0.6))  = 0.14
        _EdgeShadowSoftness ("Edge Shadow Softness (world)", Range(0.01, 1)) = 0.25

        // Opaque at rest (One/Zero: no framebuffer read). GroundView switches its own instance to premultiplied
        // (One/OneMinusSrcAlpha) while a reveal leaves part of the screen to the sky or the surface under it.
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
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
        Blend [_SrcBlend] [_DstBlend]
        // Colour only, like the floor: the GI capture's alpha is its sprite-coverage mask.
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/Ground.cginc"
            #include "../Include/SmokeField.cginc"

            sampler2D _PaperTex;
            float  _TileSize;
            half4  _PaperColor;
            half4  _FibreColor;
            half   _ToneStrength;
            half   _Relief;
            float  _UndulationSize;
            half   _Undulation;
            half   _LightInfluence;
            sampler2D _GlintTex;
            float  _GlintSize;
            half   _GlintShare;
            half   _GlintStrength;
            float  _EdgeDeckle;
            half4  _EdgeBrownColor;
            float  _EdgeBrownWidth;
            half   _EdgeBrown;
            half   _EdgeShadow;
            float  _EdgeShadowOffset;
            float  _EdgeShadowSoftness;
            float  _InkThreshold;
            float  _InkBlur;
            sampler2D _InkNoiseTex;
            float  _InkNoiseSize;
            float  _InkEdgeWarp;
            float  _InkEdgeNoise;
            half   _InkWash;
            half   _InkRim;
            float  _InkRimWidth;
            half   _InkGranulation;

            // SmokeFieldService's ink field: the smoke field's bounds, its own texels, and 1 while the level inks.
            sampler2D _InkFieldTex;
            float4 _InkFieldTexelSize;
            float  _InkFieldActive;

            // Pushed by GroundView. x = 1 while the paper opens straight over the meadow or the water (a swap: no mist,
            // the hole read from _GroundReveal.y and _GroundRevealShape while _GroundReveal.x stays 0, so every other
            // reader stays at rest); y = 1 when the paper shows inside the hole (arriving), 0 outside it (leaving).
            float4 _PaperReveal;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                // xy = the tile, zw = the second, turned and larger tap of it; both travel with the board.
                float4 tileUV   : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                float2 swellUV  : TEXCOORD2;
                float2 glintUV  : TEXCOORD3;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                float2 wp = mul(unity_ObjectToWorld, IN.vertex).xy;
                float2 paper = GroundPointAt(wp);
                float2 uv = paper / max(_TileSize, 1e-3);
                OUT.tileUV = float4(uv, float2(0.8 * uv.x - 0.6 * uv.y, 0.6 * uv.x + 0.8 * uv.y) * 0.61 + float2(0.37, 0.19));
                OUT.worldPos = wp;
                OUT.swellUV = paper / max(_UndulationSize, 1e-3) + float2(0.53, 0.11);
                OUT.glintUV = paper / max(_GlintSize, 1e-3) + float2(0.29, 0.83);
                return OUT;
            }

            // How much of the screen the paper covers at a point: the mist reveal's hole from the sky, the swap's hole
            // over a surface (frayed along the fibres by their slope, 0.5-biased), or all of it at rest.
            half Coverage(float2 worldPos, half fray)
            {
                [branch] if (_PaperReveal.x > 0.5)
                {
                    float d = GroundRevealDistance(worldPos) + (fray - 0.5h) * _EdgeDeckle;
                    float inside = 1.0 - smoothstep(-0.02, 0.02, d);
                    return (half)(_PaperReveal.y > 0.5 ? inside : 1.0 - inside);
                }

                return (half)GroundCoverageAt(worldPos);
            }

            // The ink field at a point. Four taps half a texel out, the colour weighted by each tap's opacity, so the
            // empty texels around a blot don't darken its edge.
            float4 InkFieldSample(float2 wp)
            {
                float2 uv = SmokeFieldUV(wp);
                float2 h = _InkFieldTexelSize.xy * 0.5;
                float4 a = tex2D(_InkFieldTex, uv + float2(-h.x, -h.y));
                float4 b = tex2D(_InkFieldTex, uv + float2(h.x, -h.y));
                float4 c = tex2D(_InkFieldTex, uv + float2(-h.x, h.y));
                float4 d = tex2D(_InkFieldTex, uv + float2(h.x, h.y));
                float weight = a.a + b.a + c.a + d.a;
                float3 rgb = (a.rgb * a.a + b.rgb * b.a + c.rgb * c.a + d.rgb * d.a) / max(weight, 1e-4);
                return float4(rgb, weight * 0.25);
            }

            // How much pigment a point holds, and its colour: one colour per blot out to a soft, wandering edge, a
            // light wash in the body and a stronger dried line just inside the edge.
            half InkAt(float2 wp, out half3 inkColor)
            {
                inkColor = half3(0.0h, 0.0h, 0.0h);
                [branch] if (_InkFieldActive < 0.5)
                {
                    return 0.0h;
                }

                float2 noiseUV = wp / max(_InkNoiseSize, 1e-3);
                float coarse = tex2D(_InkNoiseTex, noiseUV).r - 0.5;
                float fine = tex2D(_InkNoiseTex, noiseUV * 2.7 + float2(0.37, 0.61)).r - 0.5;
                float level = _InkThreshold + (coarse * 0.6 + fine * 0.4) * _InkEdgeNoise;

                float4 ink = InkFieldSample(wp + float2(coarse, fine) * (2.0 * _InkEdgeWarp));
                float width = max(fwidth(ink.a) * 1.2, max(_InkBlur, 1e-4));
                float body = smoothstep(level - width, level + width, ink.a);
                float rim = 1.0 - smoothstep(level, level + _InkRimWidth, ink.a);

                inkColor = (half3)ink.rgb;
                return (half)(body * saturate(_InkWash + rim * _InkRim));
            }

            half4 frag(v2f IN) : SV_Target
            {
                half4 near = tex2D(_PaperTex, IN.tileUV.xy);
                half4 far = tex2D(_PaperTex, IN.tileUV.zw);

                // The far tap's slope turned back into the near one's frame.
                half2 farSlope = far.gb * 2.0h - 1.0h;
                half2 slope = (near.gb * 2.0h - 1.0h) * 0.65h
                            + half2(0.8h * farSlope.x + 0.6h * farSlope.y, -0.6h * farSlope.x + 0.8h * farSlope.y) * 0.35h;

                // The sheet's swells, from the tile's smooth undulation read very large. The taps sit a few dozen texels
                // apart, so the 8-bit steps of the height never band the shading.
                float2 delta = float2(0.05, 0.0);
                half h0 = tex2D(_PaperTex, IN.swellUV).a;
                half hx = tex2D(_PaperTex, IN.swellUV + delta.xy).a;
                half hy = tex2D(_PaperTex, IN.swellUV + delta.yx).a;
                half2 swell = half2(hx - h0, hy - h0) * 6.0h;

                // A slope that climbs away from the light faces it. The light field's direction, so a pop rakes the
                // fibres from where it burst.
                half2 toLight = (half2)SceneLightDirectionAt(IN.worldPos);
                half facing = -dot(slope, toLight) * _Relief - dot(swell, toLight) * _Undulation;
                half shade = saturate(1.0h + facing);

                half tone = lerp(1.0h, near.r * 0.6h + far.r * 0.4h, _ToneStrength);
                half3 color = lerp(_FibreColor.rgb, _PaperColor.rgb, tone);

                // The ink glazes the page like watercolour: multiplied in, so the fibres show through and the balloons
                // over it still read in their own colour, and the relief and the light below shade it like the page.
                // The pigment settles a little deeper in the fibres' darker hollows.
                half3 inkColor;
                half ink = InkAt(IN.worldPos, inkColor);
                ink = saturate(ink * (1.0h + _InkGranulation * (1.0h - tone) * 2.0h - _InkGranulation * 0.3h));
                color *= lerp(half3(1.0h, 1.0h, 1.0h), inkColor, ink);

                // Per pixel and field-aware, like the sprites over it: the time of day, plus the local light of pops
                // and items where the light field carries it.
                half3 light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTintAt(IN.worldPos), _LightInfluence);

                half coverage = Coverage(IN.worldPos, near.g);
                half shadow = 0.0h;
                [branch] if (_PaperReveal.x > 0.5)
                {
                    // Browned just inside the edge, where the sheet ends.
                    float d = GroundRevealDistance(IN.worldPos) + (near.g - 0.5h) * _EdgeDeckle;
                    float intoPaper = _PaperReveal.y > 0.5 ? -d : d;
                    half brown = (half)(1.0 - smoothstep(0.0, _EdgeBrownWidth, intoPaper)) * _EdgeBrown;
                    color = lerp(color, _EdgeBrownColor.rgb, brown);

                    // The sheet lies on the surface under it: the paper toward the light shades this point.
                    float2 caster = IN.worldPos + (float2)toLight * _EdgeShadowOffset;
                    float dc = GroundRevealDistance(caster) + (near.g - 0.5h) * _EdgeDeckle;
                    float casterInside = 1.0 - smoothstep(-_EdgeShadowSoftness, _EdgeShadowSoftness, dc);
                    shadow = (half)(_PaperReveal.y > 0.5 ? casterInside : 1.0 - casterInside) * _EdgeShadow;
                }

                // Sparse glints: each flagged grain is a tiny facet with its own tilt, riding the paper's, and flashes
                // only when it faces the light. The mips average the speckle toward 0.5, so they fade with distance
                // rather than shimmer.
                half4 grain = tex2D(_GlintTex, IN.glintUV);
                half flagged = smoothstep(0.995h - _GlintShare, 1.005h - _GlintShare, grain.a);
                half grainFacing = saturate(-dot((grain.gb * 2.0h - 1.0h) + slope * 0.5h, toLight));
                half glint = flagged * grainFacing * grainFacing * grainFacing * grainFacing * _GlintStrength
                           * (1.0h - ink);

                half3 lit = (color * shade + glint) * light;

                // Premultiplied, so the same output is right opaque at rest (coverage 1) and blended in a reveal; the
                // shadow darkens only what the paper leaves uncovered.
                return half4(lit * coverage, coverage + (1.0h - coverage) * shadow);
            }
            ENDCG
        }
    }
}
