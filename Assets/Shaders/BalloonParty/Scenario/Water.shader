Shader "BalloonParty/Scenario/Water"
{
    // The water surface shown on water levels (GroundView's floor quad, in place of the meadow floor). Two layers
    // in one pass. Under the surface, the bottom: a deep to shallow colour drift from the shared noise, lit by
    // caustics, the light the surface focuses (WaterCausticBuffer draws it from this same surface into a small
    // soft target, read back slightly enlarged, wobbled and shifted away from the time-of-day sun, so it sits
    // below the waves rather than on them). Over it, the surface: slow noise waves and rings wherever the shared
    // disturbance field holds a push (WaterSurface.cginc), whose slopes catch a highlight. The surface travels
    // with the board like the meadow; the rings stay where the push is.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Noise (R, linear)", 2D) = "gray" {}

        [Header(Bottom)]
        _DeepColor        ("Deep Colour",                 Color)          = (0.07, 0.20, 0.30, 1)
        _ShallowColor     ("Shallow Colour",              Color)          = (0.13, 0.36, 0.44, 1)
        _DepthPatchSize   ("Depth Patch Size (world)",    Float)          = 7
        _DepthContrast    ("Depth Contrast",              Range(0, 1))    = 0.6

        [Header(Caustics)]
        _CausticColor     ("Caustic Colour",              Color)          = (0.55, 0.85, 0.90, 1)
        _CausticStrength  ("Caustic Strength",            Range(0, 2))    = 0.6
        // >1 draws the caustics larger than the waves that make them: the bottom is below the surface.
        _CausticScale     ("Caustic Enlargement",         Range(1, 2))    = 1.15
        // World units the caustics sit away from the sun: light reaches the bottom at a slant.
        _CausticShift     ("Sun Shift (world)",           Range(0, 1))    = 0.35
        // World units the bottom's own noise wobbles the caustics by, so they never trace the waves exactly.
        _CausticWobble    ("Wobble (world)",              Range(0, 0.5))  = 0.12
        _CausticWobbleSize("Wobble Size (world)",         Float)          = 3

        [Header(Surface)]
        // Copied onto the caustic pass's material by GroundView, so the caustics match these waves.
        _WaveSize         ("Wave Size (world)",           Float)          = 2.2
        _WaveSpeed        ("Wave Drift (world/s)",        Float)          = 0.12
        _WaveHeight       ("Wave Height",                 Float)          = 0.08
        // Rings across a push's intensity (0 at rest, about 1 under a fresh stamp): more = tighter rings.
        _RippleRings      ("Rings Per Push",              Float)          = 5
        _RippleSpeed      ("Ring Speed (radians/s)",      Float)          = 7
        _RippleHeight     ("Ring Height",                 Float)          = 0.12
        _HighlightColor   ("Highlight Colour",            Color)          = (0.85, 0.95, 1.0, 1)
        _HighlightStrength("Highlight Strength",          Range(0, 2))    = 0.6

        [Header(Lighting)]
        // Scene-light colour x intensity multiplies the water: 0 = authored colours always, 1 = fully lit.
        _LightInfluence   ("Light Influence",             Range(0, 1))    = 1

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
            float  _CausticScale;
            float  _CausticShift;
            float  _CausticWobble;
            float  _CausticWobbleSize;
            half4  _HighlightColor;
            half   _HighlightStrength;
            half   _LightInfluence;

            // WaterCausticBuffer's target, over the disturbance field's rect: r = focused light. Black when unset.
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

            // The light on the bottom at a world position: the surface above and toward the sun focused it there.
            // Enlarged about the camera (the field's centre), so the net reads as deeper than the waves.
            half CausticAt(float2 worldPos, float2 waterPos)
            {
                float2 wobbleUV = waterPos / max(_CausticWobbleSize, 1e-3) + float2(0.23, 0.61);
                float2 wobble = float2(tex2D(_NoiseTex, wobbleUV).r, tex2D(_NoiseTex, wobbleUV.yx + 0.5).r) - 0.5;
                float2 source = worldPos / max(_CausticScale, 1e-3)
                              + _SceneLightDir.xy * _CausticShift
                              + wobble * 2.0 * _CausticWobble;
                float2 uv = (source - _FieldBoundsMin) / max(_FieldBoundsSize, 1e-4);
                return (half)tex2D(_WaterCausticTex, uv).r;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.world;
                float2 waterPos = GroundPointAt(wp);

                half depthNoise = tex2D(_NoiseTex, waterPos / max(_DepthPatchSize, 1e-3)).r;
                half depthMix = saturate(0.5h + (depthNoise - 0.5h) * (1.0h + 2.0h * _DepthContrast));
                half3 color = lerp(_DeepColor.rgb, _ShallowColor.rgb, depthMix);
                color += _CausticColor.rgb * CausticAt(wp, waterPos) * _CausticStrength;

                // Slopes facing the sun catch its highlight, those facing away darken a little: the surface reads
                // in relief over the bottom.
                float2 slope = WaterSlope(wp);
                half facing = (half)dot(slope, _SceneLightDir.xy);
                color += _HighlightColor.rgb * saturate(facing) * _HighlightStrength;
                color *= 1.0h - saturate(-facing) * 0.25h * _HighlightStrength;

                // Per pixel and field-aware, like the meadow floor: the time of day plus the local light of pops.
                half3 light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTintAt(wp), _LightInfluence);

                // Premultiplied, so the same output is right opaque at rest (coverage 1) and blended in a reveal.
                half coverage = (half)GroundCoverageAt(wp);
                return half4(color * light * coverage, coverage);
            }
            ENDCG
        }
    }
}
