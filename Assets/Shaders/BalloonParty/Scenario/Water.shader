Shader "BalloonParty/Scenario/Water"
{
    // The water surface shown on water levels (GroundView's floor quad, in place of the meadow floor). Three layers
    // in one pass. At the bottom, a shallow-to-deep colour drift lit by the caustic net: WaterCausticBuffer's
    // moving tile, laid twice at two scales and angles (one draws the lines, the softer other varies their
    // brightness, so the net never looks tiled), a little enlarged and shifted away from the time-of-day sun,
    // split into colour fringes along it, and blurred where the water is deep. The bottom is seen through the
    // surface's slope, so the waves and rings over it bend the net. Over it, a light veil of water colour, then the
    // surface: slow noise waves and rings wherever the shared disturbance field holds a push (WaterSurface.cginc),
    // whose slopes catch a soft highlight, and whose crests turned to the sun glint. The surface travels
    // with the board like the meadow; the rings stay where the push is.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, linear)", 2D) = "gray" {}

        [Header(Bottom)]
        _DeepColor        ("Deep Colour",                 Color)          = (0.16, 0.50, 0.60, 1)
        _ShallowColor     ("Shallow Colour",              Color)          = (0.40, 0.76, 0.78, 1)
        _DepthPatchSize   ("Depth Patch Size (world)",    Float)          = 12
        _DepthContrast    ("Depth Contrast",              Range(0, 1))    = 0.25

        [Header(Caustics)]
        _CausticColor     ("Caustic Colour",              Color)          = (0.93, 1.0, 0.97, 1)
        _CausticStrength  ("Caustic Strength",            Range(0, 1))    = 0.7
        // World units one caustic tile covers: its cells are this over the tile's cell count.
        _CausticTileSize  ("Tile Size (world)",           Float)          = 4.5
        // The second layer's size against the first: never a whole number, so the two never line up.
        _CausticSecondScale("Second Layer Scale",         Range(1, 3))    = 1.73
        // World units the caustics sit away from the sun: light reaches the bottom at a slant.
        _CausticShift     ("Sun Shift (world)",           Range(0, 1))    = 0.35
        // World units the colours of a caustic line part by along the sun, as light splits through water.
        _CausticSplit     ("Colour Split (world)",        Range(0, 0.1))  = 0.025
        // World units the bottom's own noise wobbles the caustics by, so they never sit still on the grid.
        _CausticWobble    ("Wobble (world)",              Range(0, 0.5))  = 0.1
        _CausticWobbleSize("Wobble Size (world)",         Float)          = 3
        // Mip levels the net blurs by over the deepest water.
        _CausticDepthBlur ("Depth Blur (mips)",           Range(0, 4))    = 1.5

        [Header(Surface)]
        _WaveSize         ("Wave Size (world)",           Float)          = 2.2
        _WaveSpeed        ("Wave Drift (world/s)",        Float)          = 0.12
        _WaveHeight       ("Wave Height",                 Float)          = 0.06
        // Rings across a push's intensity (0 at rest, about 1 under a fresh stamp): more = tighter rings.
        _RippleRings      ("Rings Per Push",              Float)          = 5
        _RippleSpeed      ("Ring Speed (radians/s)",      Float)          = 7
        _RippleHeight     ("Ring Height",                 Float)          = 0.05
        // World units the surface slope shifts the view of the bottom by: the waves bend what lies under them.
        _Refraction       ("Refraction (world)",          Range(0, 1))    = 0.22
        // The water body over the bottom: a light veil that tints and softens what lies under it.
        _SurfaceColor     ("Surface Colour",              Color)          = (0.50, 0.78, 0.82, 1)
        _SurfaceOpacity   ("Surface Opacity",             Range(0, 1))    = 0.15
        _HighlightColor   ("Highlight Colour",            Color)          = (0.92, 0.98, 1.0, 1)
        _HighlightStrength("Highlight Strength",          Range(0, 1))    = 0.2

        [Header(Glints)]
        // How much steeper the swell and the rings stand to the sun than to the eye: more = more of their crests
        // turned to the sun.
        _GlintSteepness   ("Glint Steepness",             Range(0, 10))   = 3
        // How exactly a crest must face the sun to glint: more = fewer, narrower glints.
        _GlintPower       ("Glint Tightness",             Float)          = 90
        _GlintStrength    ("Glint Strength",              Range(0, 2))    = 0.55

        [Header(Lighting)]
        // Scene-light colour x intensity multiplies the water: 0 = authored colours always, 1 = fully lit. Below 1,
        // so a warm evening tints the water rather than turning it olive.
        _LightInfluence   ("Light Influence",             Range(0, 1))    = 0.7

        // Opaque at rest (One/Zero: no framebuffer read). GroundView switches its own instance to premultiplied
        // (One/OneMinusSrcAlpha) while the mist reveal leaves part of the screen to the sky.
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
        // Colour only, like the meadow floor: SceneCaptureService captures the backdrop for the GI bounce, and its
        // alpha is the capture's sprite-coverage mask; a full-screen surface writing it would read as an occluder.
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/WaterSurface.cginc"

            half4  _DeepColor;
            half4  _ShallowColor;
            float  _DepthPatchSize;
            half   _DepthContrast;
            half4  _CausticColor;
            half   _CausticStrength;
            float  _CausticTileSize;
            float  _CausticSecondScale;
            float  _CausticShift;
            float  _CausticSplit;
            float  _CausticWobble;
            float  _CausticWobbleSize;
            float  _CausticDepthBlur;
            float  _Refraction;
            half4  _SurfaceColor;
            half   _SurfaceOpacity;
            half4  _HighlightColor;
            half   _HighlightStrength;
            float  _GlintSteepness;
            float  _GlintPower;
            half   _GlintStrength;
            half   _LightInfluence;

            // WaterCausticBuffer's wrapping tile, with mips: r = focused light. Black when unset.
            sampler2D _WaterCausticTex;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 world  : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.world = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            // The light on the bottom at a point of it (it travels with the board), per colour. The first layer
            // draws the lines; the second, turned and larger and read softer, brightens them where its own lines
            // cross, so the lines vary along their length the way real caustics do and the tile never repeats
            // visibly. Blurred by up to _CausticDepthBlur mips where the water is deep.
            half3 CausticAt(float2 bottomPos, half deep)
            {
                float2 wobbleUV = bottomPos / max(_CausticWobbleSize, 1e-3) + float2(0.23, 0.61);
                float2 wobble = float2(tex2D(_NoiseTex, wobbleUV).r, tex2D(_NoiseTex, wobbleUV.yx + 0.5).r) - 0.5;
                float2 p = bottomPos + _SceneLightDir.xy * _CausticShift + wobble * 2.0 * _CausticWobble;

                float tile = max(_CausticTileSize, 1e-3);
                float blur = deep * _CausticDepthBlur;
                float2 uv = p / tile;
                float2 split = _SceneLightDir.xy * (_CausticSplit / tile);
                half3 lines = half3(
                    tex2Dbias(_WaterCausticTex, float4(uv + split, 0.0, blur)).r,
                    tex2Dbias(_WaterCausticTex, float4(uv, 0.0, blur)).r,
                    tex2Dbias(_WaterCausticTex, float4(uv - split, 0.0, blur)).r);

                float2 turned = float2(0.8 * p.x - 0.6 * p.y, 0.6 * p.x + 0.8 * p.y);
                float2 uvSecond = turned / (tile * _CausticSecondScale) + float2(0.37, 0.71);
                half second = tex2Dbias(_WaterCausticTex, float4(uvSecond, 0.0, blur + 2.0)).r;

                return lines * (0.45h + 1.4h * second);
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.world;
                float2 waterPos = GroundPointAt(wp);

                // The bottom is seen through the surface: its slope bends the view, so the waves and rings over it
                // ripple the bottom and its caustics rather than drawing beside them.
                float2 slope = WaterSlope(wp);
                float2 bottomPos = waterPos + slope * _Refraction;

                half depthNoise = tex2D(_NoiseTex, bottomPos / max(_DepthPatchSize, 1e-3)).r;
                half depthMix = saturate(0.5h + (depthNoise - 0.5h) * (1.0h + 2.0h * _DepthContrast));
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthMix);

                // Blended toward, never added: the light stays inside the bottom's range, so nothing blooms.
                // Deeper patches catch less of it, and softer.
                half3 caustic = CausticAt(bottomPos, 1.0h - depthMix)
                              * (_CausticStrength * lerp(0.55h, 1.0h, depthMix));
                color = lerp(color, _CausticColor.rgb, saturate(caustic));

                // The water body between the bottom and the surface veils both.
                color = lerp(color, _SurfaceColor.rgb, _SurfaceOpacity);

                // The swell's and the rings' slopes facing the sun catch a soft highlight; those facing away darken.
                half facing = (half)dot(slope, _SceneLightDir.xy);
                color = lerp(color, _HighlightColor.rgb, saturate(facing * _HighlightStrength));
                color *= 1.0h - saturate(-facing) * 0.25h * _HighlightStrength;

                // Glints: a crest of the swell or of a ring turned just so between the sun and the eye flashes. Fine
                // noise waves were tried for sparkles and only read as static.
                float3 normal = normalize(float3(-slope * _GlintSteepness, 1.0));
                float3 toSun = normalize(float3(_SceneLightDir.xy, 0.8));
                float3 halfway = normalize(toSun + float3(0.0, 0.0, 1.0));
                half glint = (half)pow(saturate(dot(normal, halfway)), _GlintPower) * _GlintStrength;
                color += _HighlightColor.rgb * glint;

                // Per pixel and field-aware, like the meadow floor: the time of day plus the local light of pops.
                // Light above full is eased in, because the pale water would clip a pop's glow to flat white.
                half3 light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTintAt(wp), _LightInfluence);
                light /= 1.0h + max(light - 1.0h, 0.0h) * 0.7h;

                // Premultiplied, so the same output is right opaque at rest (coverage 1) and blended in a reveal.
                half coverage = (half)GroundCoverageAt(wp);
                return half4(color * light * coverage, coverage);
            }
            ENDCG
        }
    }
}
