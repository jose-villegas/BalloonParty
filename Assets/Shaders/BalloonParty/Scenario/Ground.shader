Shader "BalloonParty/Scenario/Ground"
{
    // The ground backdrop on ground levels (GroundView's quad, sorted between the sky scatter and the
    // cloud backdrop): a top-down meadow that fully covers its pixels. Everything is read from baked
    // data — the shared tileable cloud noise for the large patches, a small fine-grained stroke texture
    // for the blades, and the shared disturbance field for the reaction — so the per-pixel cost is four
    // taps and no procedural noise.
    //
    // Blades are two layers of the same stretched-noise stroke pattern: the base where the blade is
    // rooted, and its tip read a short way back along a lean vector, so the bright tips sit offset from
    // their darker bases. The lean is a gentle idle sway plus the disturbance field's displacement, and
    // seen from above a blade pushed over shows more of its length: where a ball (or a pop, a bomb, a
    // laser) stamped the field, the tips swing along the push, the standing tips thin out and the laid
    // blades catch the sun, leaving a brushed stripe that springs back as the field reforms.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Patch Noise (R, linear)", 2D) = "gray" {}
        // Fine, equalised, tileable noise (with mips): stretched along the blades it reads as strokes. The
        // cloud noise is too soft for this — its blobs stretch into camouflage, not grass.
        [NoScaleOffset] _BladeTex ("Blade Strokes (R, linear)", 2D) = "gray" {}

        [Header(Meadow)]
        _SoilColor      ("Under Colour",                 Color)           = (0.14, 0.27, 0.11, 1)
        _GrassColor     ("Grass Colour",                 Color)           = (0.29, 0.50, 0.19, 1)
        _TipColor       ("Tip Colour",                   Color)           = (0.60, 0.77, 0.33, 1)
        _PatchSize      ("Patch Size (world units)",     Float)           = 3.5
        _PatchContrast  ("Patch Contrast",               Range(0, 1))     = 0.5

        [Header(Blades)]
        // Stroke-texture tiles per world unit across the blades; the stretch divides it along them.
        _BladeTiling    ("Blade Tiling",                 Float)           = 1
        _BladeStretch   ("Blade Stretch",                Range(1, 12))    = 5
        _BladeAngle     ("Rest Lean Angle (degrees)",    Range(0, 360))   = 100
        _BladeLength    ("Blade Length (world units)",   Range(0, 0.4))   = 0.08
        _TipThreshold   ("Tip Threshold",                Range(0, 1))     = 0.55
        _TipSoftness    ("Tip Softness",                 Range(0.01, 0.5))= 0.12
        _TipCoverage    ("Tip Coverage",                 Range(0, 1))     = 0.8

        [Header(Idle Sway)]
        _SwayAmount     ("Sway Amount",                  Range(0, 1))     = 0.35
        _SwaySpeed      ("Sway Speed",                   Range(0, 4))     = 0.8

        [Header(Wake)]
        // How far the field's displacement swings the tips, in blade lengths.
        _WakeLean       ("Wake Lean",                    Range(0, 8))     = 3
        // How much a stirred field lays the standing tips down.
        _WakeFlatten    ("Wake Flatten",                 Range(0, 1))     = 0.6
        _WakeSheen      ("Wake Sheen",                   Range(0, 1))     = 0.3
        _SheenColor     ("Sheen Colour",                 Color)           = (0.85, 0.92, 0.60, 1)

        [Header(Lighting)]
        // Scene-light colour x intensity multiplies the meadow: 0 = authored colours always, 1 = fully lit.
        _LightInfluence ("Light Influence",              Range(0, 1))     = 1

        // Opaque by default (One/Zero: no framebuffer read). Exposed so a sky/ground edge can switch the
        // quad to premultiplied blending while it is partly covered.
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

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            sampler2D _NoiseTex;
            sampler2D _BladeTex;
            half4  _SoilColor;
            half4  _GrassColor;
            half4  _TipColor;
            float  _PatchSize;
            half   _PatchContrast;
            float  _BladeTiling;
            float  _BladeStretch;
            float  _BladeAngle;
            float  _BladeLength;
            half   _TipThreshold;
            half   _TipSoftness;
            half   _TipCoverage;
            half   _SwayAmount;
            half   _SwaySpeed;
            half   _WakeLean;
            half   _WakeFlatten;
            half   _WakeSheen;
            half4  _SheenColor;
            half   _LightInfluence;

            // Globals pushed by DisturbanceFieldService (Shared/Disturbance) — kept out of Properties so a
            // material default can't mask them.
            sampler2D _DisturbanceTex;
            float2    _FieldBoundsMin;
            float2    _FieldBoundsSize;

            // Pushed by GroundView: the scenario root's displacement. The meadow is the floor the bushes
            // stand on, so on the Ascent (and the restart rise) it travels with them one-to-one instead of
            // the bushes sliding over a still ground.
            float2    _GroundScroll;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            // Everything linear in world position, or constant per material, is worked out per vertex: the
            // interpolated values are exact, and the fragment keeps only the taps and what depends on them.
            struct v2f
            {
                float4 vertex     : SV_POSITION;
                // xy = disturbance-field UV, zw = patch-noise UV.
                float4 fieldPatch : TEXCOORD0;
                // xy = blade-stroke UV of the blade's root, zw = toward the sun.
                float4 bladeSun   : TEXCOORD1;
                // Blade-UV axes, pre-scaled: xy maps a world offset along the blades, zw across them.
                float4 bladeAxes  : TEXCOORD2;
                // rgb = the scene-light multiplier, a = the rest lean as a blade-UV step along the blades.
                float4 lightLean  : TEXCOORD3;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                float2 wp = mul(unity_ObjectToWorld, IN.vertex).xy;

                // The disturbance field covers the camera's rect; a field that hasn't pushed its bounds yet
                // (size 0) maps everything far outside it, so the grass rests.
                // The field is camera-fixed (shots stamp where they are on screen); the meadow pattern is
                // read where the ground has travelled to.
                float2 fieldUV = (wp - _FieldBoundsMin) / max(_FieldBoundsSize, 1e-4);
                float2 gp = wp - _GroundScroll;
                OUT.fieldPatch = float4(fieldUV, gp / max(_PatchSize, 1e-3));

                // Stroke-pattern UV: tight across the blades, stretched along them, so the tileable noise
                // reads as long thin strokes.
                float s, c;
                sincos(radians(_BladeAngle), s, c);
                float2 along = float2(c, s) * (_BladeTiling / max(_BladeStretch, 1.0));
                float2 across = float2(-s, c) * _BladeTiling;
                OUT.bladeAxes = float4(along, across);
                OUT.bladeSun = float4(dot(gp, across), dot(gp, along), SceneLightDirection());

                // One blade length along the rest lean is (0, tiling / stretch x length) in blade UV.
                float3 light = lerp(float3(1.0, 1.0, 1.0), SceneLightTint(), _LightInfluence);
                OUT.lightLean = float4(light, _BladeTiling / max(_BladeStretch, 1.0) * _BladeLength);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                // Outside the field's rect (the overscan past the camera) the grass rests.
                float2 fieldUV = IN.fieldPatch.xy;
                half inField = all(fieldUV == saturate(fieldUV)) ? 1.0h : 0.0h;
                half3 field = tex2D(_DisturbanceTex, fieldUV).rgb;
                half2 push = (field.gb - 0.5h) * 2.0h * inField;
                half stir = saturate(abs(field.r - 0.5h) * 2.0h) * inField;

                // Large soft patches of lusher and drier grass.
                half patch = tex2D(_NoiseTex, IN.fieldPatch.zw).r;
                half patchMix = saturate(0.5h + (patch - 0.5h) * (1.0h + 2.0h * _PatchContrast));
                half3 grass = lerp(_SoilColor.rgb, _GrassColor.rgb, patchMix);

                // Scaled time, like the clouds, so the meadow pauses with them. The patch value phases the
                // sway so neighbouring patches don't swing in lockstep.
                half sway = sin(_Time.y * _SwaySpeed + patch * 6.2832h) * _SwayAmount;

                // The tip sits back along the lean from its root: the rest lean (along the blades, swaying)
                // plus the field's push, turned into blade UV.
                float2 pushUV = float2(dot(push, IN.bladeAxes.zw), dot(push, IN.bladeAxes.xy))
                    * (_WakeLean * _BladeLength);
                float2 rootUV = IN.bladeSun.xy;
                float2 tipUV = rootUV - float2(0.0, IN.lightLean.a * (1.0h + sway)) - pushUV;

                half under = tex2D(_BladeTex, rootUV).r;
                half tip = tex2D(_BladeTex, tipUV).r;

                half3 col = lerp(_SoilColor.rgb, grass, smoothstep(0.2h, 0.7h, under));

                half tipMask = smoothstep(_TipThreshold, _TipThreshold + _TipSoftness, tip)
                    * _TipCoverage * (1.0h - stir * _WakeFlatten);
                col = lerp(col, _TipColor.rgb * lerp(0.85h, 1.1h, patch), tipMask);

                // Laid-down blades show their sides: brighter where they lean toward the sun.
                half2 pushDir = push / max(length(push), 1e-3h);
                half facing = saturate(dot(pushDir, (half2)IN.bladeSun.zw) * 0.5h + 0.5h);
                col += _SheenColor.rgb * (stir * _WakeSheen * facing);

                col *= (half3)IN.lightLean.rgb;
                return half4(col, 1.0h);
            }
            ENDCG
        }
    }
}
