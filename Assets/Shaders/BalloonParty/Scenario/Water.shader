Shader "BalloonParty/Scenario/Water"
{
    // The water surface shown on water levels (GroundView's floor quad, in place of the meadow floor), drawn as a
    // body of water over a sandy bottom, in one pass. The bottom's depth drifts across the level, from sandbanks to
    // deep channels. It is seen as from a camera above the water: the deeper it lies, the more it shrinks toward
    // the middle of the screen and the slower it slides when the board scrolls, and the more the surface's slope
    // bends the view of it. Its sand carries ripple bands and the caustic net (WaterCausticBuffer's moving tile, laid
    // twice at two scales and angles, shifted away from the time-of-day sun, split into colour fringes and blurred
    // where it's deep). The water over it absorbs red first, then green, and scatters its own blue back, so shallows
    // read as sand and deep water as teal. On top, the surface: a slow swell plus the waves the water simulation
    // (WaterRippleBuffer) sets moving at every impact (WaterSurface.cginc). Each wave throws a bright and a dark band
    // on the sand and sheens the surface whatever the sun's angle, and its crest reads lighter than its trough; the
    // slopes also catch a soft highlight, crests turned to the sun glint, and the simulation's foam lies on top.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, linear)", 2D) = "gray" {}

        [Header(Bottom)]
        _SandColor        ("Sand Colour",                 Color)          = (0.80, 0.78, 0.67, 1)
        // World size of the sandbanks and channels.
        _DepthPatchSize   ("Depth Patch Size (world)",    Float)          = 9
        // How sharply the bottom drops from the banks into the channels: 0 = a gentle drift, 1 = shelves.
        _DepthContrast    ("Depth Contrast",              Range(0, 1))    = 0.4
        _ShallowDepth     ("Shallowest (world)",          Float)          = 0.6
        _DeepDepth        ("Deepest (world)",             Float)          = 4.5
        // World units between the sand's ripple bands, and how much they shade it.
        _SandRippleSize   ("Sand Ripple Size (world)",    Float)          = 0.7
        _SandRippleStrength("Sand Ripple Strength",       Range(0, 0.5))  = 0.12

        [Header(Water Body)]
        // The colour the water scatters back: what deep water turns to.
        _WaterColor       ("Water Colour",                Color)          = (0.10, 0.40, 0.60, 1)
        // How much of each colour a world unit of water takes out of the bottom: red goes first, blue last.
        _Absorption       ("Absorption RGB (per world)",  Vector)         = (0.45, 0.14, 0.09, 0)
        // How much the bottom shrinks toward the middle of the screen per world unit of depth: the camera's
        // perspective, 1 over its height above the water.
        _Perspective      ("Perspective (per depth)",     Range(0, 0.1))  = 0.035

        [Header(Caustics)]
        _CausticColor     ("Caustic Colour",              Color)          = (1.0, 1.0, 0.94, 1)
        _CausticStrength  ("Caustic Strength",            Range(0, 1))    = 0.45
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
        _CausticDepthBlur ("Depth Blur (mips)",           Range(0, 4))    = 1.8

        [Header(Surface)]
        _WaveSize         ("Swell Size (world)",          Float)          = 2.2
        _WaveSpeed        ("Swell Speed (world/s)",       Float)          = 0.25
        _WaveHeight       ("Swell Height",                Float)          = 0.04
        // How tall the simulated waves stand, in world units per rest depth of the simulation (their speed, tension
        // and fade are on the ripple material).
        _RippleHeight     ("Wave Height",                 Float)          = 0.4
        // How much a wave focuses light on the bottom under its trough and spreads it under its crest: the bright
        // and dark bands a ripple throws on the sand, whatever the sun's angle.
        _RippleFocus      ("Wave Focus",                  Float)          = 4
        // How much a wave's slope sheens the surface, whatever the sun's angle.
        _RippleSheen      ("Wave Sheen",                  Float)          = 3
        // How much lighter a crest and darker a trough reads, per rest depth of height: the water's bulk rising
        // and sinking.
        _CrestShade       ("Crest Shade",                 Float)          = 1.4
        // World units the surface slope shifts the view of the bottom by, per world unit of depth: the deeper the
        // bottom, the more the waves bend it.
        _Refraction       ("Refraction (per depth)",      Range(0, 0.5))  = 0.2
        // A light veil of water colour over everything, so even the shallows read as wet.
        _SurfaceColor     ("Surface Colour",              Color)          = (0.50, 0.78, 0.82, 1)
        _SurfaceOpacity   ("Surface Opacity",             Range(0, 1))    = 0.08
        _HighlightColor   ("Highlight Colour",            Color)          = (0.92, 0.98, 1.0, 1)
        _HighlightStrength("Highlight Strength",          Range(0, 1))    = 0.25

        [Header(Glints)]
        // How much steeper the swell and the rings stand to the sun than to the eye: more = more of their crests
        // turned to the sun.
        _GlintSteepness   ("Glint Steepness",             Range(0, 10))   = 3
        // How exactly a crest must face the sun to glint: more = fewer, narrower glints.
        _GlintPower       ("Glint Tightness",             Float)          = 90
        _GlintStrength    ("Glint Strength",              Range(0, 2))    = 0.55

        [Header(Foam)]
        // The foam the simulation carries (breaking crests, big splashes), drawn as a lace that thins into islands
        // and specks as it fades.
        _FoamColor        ("Foam Colour",                 Color)          = (0.95, 0.98, 1.0, 1)
        _FoamStrength     ("Foam Strength",               Range(0, 1))    = 0.9
        // World units across one of the lace's holes.
        _FoamSize         ("Foam Lace Size (world)",      Float)          = 0.8
        // How soft the lace's edges are.
        _FoamSoftness     ("Foam Softness",               Range(0.01, 0.5)) = 0.25

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

            #define TAU 6.2831853

            half4  _SandColor;
            float  _DepthPatchSize;
            half   _DepthContrast;
            float  _ShallowDepth;
            float  _DeepDepth;
            float  _SandRippleSize;
            half   _SandRippleStrength;
            half4  _WaterColor;
            float4 _Absorption;
            float  _Perspective;
            half4  _CausticColor;
            half   _CausticStrength;
            float  _CausticTileSize;
            float  _CausticSecondScale;
            float  _CausticShift;
            float  _CausticSplit;
            float  _CausticWobble;
            float  _CausticWobbleSize;
            float  _CausticDepthBlur;
            float  _RippleFocus;
            float  _RippleSheen;
            float  _Refraction;
            half4  _SurfaceColor;
            half   _SurfaceOpacity;
            half4  _HighlightColor;
            half   _HighlightStrength;
            float  _GlintSteepness;
            float  _GlintPower;
            half   _GlintStrength;
            half   _CrestShade;
            half4  _FoamColor;
            half   _FoamStrength;
            float  _FoamSize;
            half   _FoamSoftness;
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

                return lines * (0.7h + 0.7h * second);
            }

            // The sand: ripple bands bent by the noise, as the current leaves them, over a mottle.
            half3 SandAt(float2 bottomPos)
            {
                float size = max(_SandRippleSize, 1e-3);
                half mottle = tex2D(_NoiseTex, bottomPos / (size * 5.0) + float2(0.53, 0.29)).r;
                half bands = (half)sin(dot(bottomPos, float2(0.83, 0.55)) / size * TAU + mottle * 9.0);
                return _SandColor.rgb * (lerp(0.86h, 1.06h, mottle) + bands * _SandRippleStrength);
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.world;
                float4 ripple = WaterRippleAt(wp);
                float2 slope = WaterSlope(wp, ripple);

                // How deep the bottom lies under this pixel: 0 = the shallowest bank, 1 = the deepest channel.
                half depthNoise = tex2D(_NoiseTex, GroundPointAt(wp) / max(_DepthPatchSize, 1e-3)).r;
                half deep = saturate(0.5h + (depthNoise - 0.5h) * (1.0h + 3.0h * _DepthContrast));
                float depth = lerp(_ShallowDepth, _DeepDepth, deep);

                // Seen from a camera above the middle of the screen (the world origin): a deeper bottom shows a wider
                // patch of itself, so it shrinks toward the middle and slides slower than the surface when the board
                // scrolls. Through the surface: its slope bends the view, more over deeper water.
                float2 bottomPos = GroundPointAt(wp * (1.0 + depth * _Perspective)) + slope * (depth * _Refraction);

                // The sand and the light the surface focuses on it, which reaches the deep bottom weaker and softer.
                // Shaded around the sand's own colour, so nothing blooms.
                half3 caustic = CausticAt(bottomPos, deep) * (_CausticStrength * lerp(1.0h, 0.45h, deep));
                half3 bottom = SandAt(bottomPos) * (1.0h - 0.3h * _CausticStrength)
                             * lerp(half3(1.0h, 1.0h, 1.0h), _CausticColor.rgb * 1.6h, saturate(caustic));
                bottom *= 1.0h + clamp((half)ripple.z * _RippleFocus, -0.25h, 0.7h);

                // The water between: each colour fades out of the bottom by its own absorption, and the water's
                // own colour takes its place.
                half3 transmit = (half3)exp(-_Absorption.rgb * depth);
                half3 color = bottom * transmit + _WaterColor.rgb * (1.0h - transmit);

                color = lerp(color, _SurfaceColor.rgb, _SurfaceOpacity);
                color *= 1.0h + clamp((half)ripple.w * _CrestShade, -0.35h, 0.35h);
                color = lerp(color, _HighlightColor.rgb, saturate((half)length(ripple.xy) * _RippleSheen - 0.05h));

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

                // Foam: two octaves of noise make a lace; the thicker the foam, the more of the lace it fills, so
                // fading foam breaks up into islands and specks. It rides the simulated surface, so it sits on top
                // of the water, unbent by it.
                half foam = (half)WaterFoamAt(wp);
                float2 lacePos = wp / max(_FoamSize, 1e-3);
                half lace = tex2D(_NoiseTex, lacePos * 0.5 + float2(0.19, 0.83)).r * 0.7h
                          + tex2D(_NoiseTex, lacePos * 1.3 + float2(0.71, 0.07)).r * 0.3h;
                half cover = smoothstep(1.0h - foam - _FoamSoftness, 1.0h - foam + _FoamSoftness, lace);
                color = lerp(color, _FoamColor.rgb, cover * smoothstep(0.02h, 0.25h, foam) * _FoamStrength);

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
