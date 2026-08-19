Shader "BalloonParty/Display/SkyScatter"
{
    // The sky-scatter backdrop: renders on the quad SkyScatterService builds at startup, sorted
    // behind every other backdrop layer (BackgroundCloud, WallNet, SmokeField) so it reads as
    // enriching the camera's flat clear colour rather than replacing it outright — this shader is
    // genuinely alpha-blended (see Blend below), so wherever it fades to transparent the camera's own
    // clear colour (already the current time-of-day light colour, via CameraBackgroundTint) shows
    // through. A naive 2D stand-in for atmospheric scattering: a stack of circles that all share one
    // tangent point toward the scene light. Every ring is coloured off the SAME live light colour —
    // there is no separate authored "horizon" hue to blend toward — with the authored accent colour
    // applied only as a multiplier whose strength fades out toward the bigger rings, and the ring's
    // own opacity fading the same way, so the effect reads as a glow near the light that dissolves
    // into the ordinary sky rather than a colour gradient with a hard destination.
    Properties
    {
        [Header(Rings)]
        _RingCount        ("Ring Count",       Range(2, 10))       = 7
        _BaseRadius       ("Base Radius",      Range(0.05, 1))     = 0.16
        _RadiusGrowth     ("Radius Growth",    Range(1.05, 3))     = 1.55
        // Scales the play-area rectangle used to place the anchor: 1 = anchor sits exactly where the
        // scene-light ray exits that rectangle (on the wall), < 1 pulls it inside the play field,
        // > 1 pushes it out beyond the wall.
        _AnchorRectScale  ("Anchor Rect Scale",Range(0, 3))        = 1

        [Header(Color)]
        // Multiplies the live scene-light colour near the origin; its influence fades toward the
        // outer rings (see _TintFalloff), so the outermost ring reads as the plain light colour.
        _SelectedColor    ("Selected Color",   Color)              = (1.15, 1.0, 0.85, 1)
        _TintFalloff      ("Tint Falloff",     Range(0.2, 3))      = 1

        [Header(Alpha)]
        // Opacity at the smallest (innermost) ring — the established base the outer rings fade down
        // from, not necessarily fully opaque.
        _BaseAlpha        ("Base Alpha",       Range(0, 1))        = 0.85
        _AlphaFalloff     ("Alpha Falloff",    Range(0.2, 4))      = 1.5

        [Header(Motion)]
        _WaveAmplitude    ("Wave Amplitude",   Range(0, 0.3))      = 0.05
        _WaveSpeed        ("Wave Speed",       Range(0, 2))        = 0.2
        _WavePhaseStep    ("Wave Phase Step",  Range(0, 6.283))    = 0.9

        [Header(Cloud Influence)]
        _CloudDistortion  ("Cloud Distortion", Range(0, 0.3))      = 0.06
        _EdgeSoftness     ("Edge Softness",    Range(0.001, 0.2))  = 0.035
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
        // One, not SrcAlpha: the fragment shader accumulates rings as PREMULTIPLIED colour (see the
        // compositing loop below), so the GPU must add it straight over the framebuffer rather than
        // multiplying by alpha a second time — SrcAlpha here would square every ring's alpha, crushing
        // anything short of fully opaque toward black and reading as "darker the bigger the circle."
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/BackgroundField.cginc"

            // Compile-time loop bound — the runtime ring count (_RingCount) only shortens it via the
            // early-continue below, mirroring the batched field shaders' _StampCount pattern.
            #define SKY_SCATTER_MAX_RINGS 10

            fixed4 _SelectedColor;
            float  _TintFalloff;
            float  _BaseAlpha;
            float  _AlphaFalloff;
            float  _RingCount;
            float  _BaseRadius;
            float  _RadiusGrowth;
            float  _AnchorRectScale;
            float  _WaveAmplitude;
            float  _WaveSpeed;
            float  _WavePhaseStep;
            float  _CloudDistortion;
            float  _EdgeSoftness;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 worldPos : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;

                // Reuses the cloud field's own world-space bounds (already pushed globally by
                // BackgroundFieldService, the same box DisturbanceFieldCoordinates builds) as "the
                // play-area rectangle" instead of a second authored size — zero when the field hasn't
                // run yet (edit time), which degrades to every ring sharing the origin: still a valid,
                // harmless concentric fallback.
                float2 halfFrame = _BackgroundFieldBoundsSize.xy * 0.5;
                float2 toLight = SceneLightDirection();
                float2 spreadDir = -toLight;

                // The anchor sits where the ray from centre along toLight exits the rectangle — i.e.
                // on the wall itself, in the light's direction — scaled by _AnchorRectScale before the
                // intersection so the knob reads as "how big a rectangle to aim at," not an arbitrary
                // distance: 1 = the wall, < 1 = inside the play field, > 1 = beyond the wall. Standard
                // ray-vs-box-boundary distance from the centre (division guarded against an
                // axis-aligned light direction).
                float2 rect = halfFrame * _AnchorRectScale;
                float2 tAxis = rect / max(abs(toLight), 1e-5);
                float  anchorReach = min(tAxis.x, tAxis.y);
                float2 anchor = toLight * anchorReach;

                // One shared cloud tap, reused (scaled per-ring below) to bend every ring's edge —
                // gated by _BackgroundFieldActive so an absent field contributes exactly zero rather
                // than a constant -0.5 bias.
                float cloudNoise = (BackgroundFieldNoise(wp) - 0.5) * _BackgroundFieldActive;

                int count = (int) clamp(_RingCount, 2, SKY_SCATTER_MAX_RINGS);
                float3 lightColor = SceneLightTint();

                // Standard back-to-front "over" compositing, starting fully transparent — wherever the
                // accumulated alpha ends below 1, Blend One OneMinusSrcAlpha lets the camera's own
                // clear colour (already the current light colour) show through underneath. Every ring
                // shares the same anchor tangent point (its centre sits exactly one radius from the
                // anchor), so growing radii nest automatically — no per-ring containment test needed.
                float4 accum = float4(0.0, 0.0, 0.0, 0.0);

                for (int j = SKY_SCATTER_MAX_RINGS - 1; j >= 0; j--)
                {
                    if (j >= count)
                    {
                        continue;
                    }

                    // count is clamped to >= 2 above, so the denominator is never zero.
                    float t = (float) j / (float) (count - 1);
                    // Scaled time, matching BackgroundFieldDensity's own clock: the sky freezes with
                    // the clouds it composites with (e.g. during the level-up popup's TimeScale 0)
                    // instead of the two backdrops drifting out of sync.
                    float phase = _Time.y * _WaveSpeed + j * _WavePhaseStep;
                    float wave = sin(phase) * _WaveAmplitude;

                    float radius = _BaseRadius * pow(_RadiusGrowth, (float) j) * (1.0 + wave);
                    float2 center = anchor + spreadDir * radius;

                    float dist = distance(wp, center) + cloudNoise * _CloudDistortion * radius;
                    float spatialCoverage = smoothstep(radius + _EdgeSoftness, radius - _EdgeSoftness, dist);

                    // near-to-far falloff (1 at the origin ring, 0 at the outermost) shapes both how
                    // strongly the selected colour tints this ring and how opaque it is — two
                    // independent knobs on the same shape, since one is a colour multiplier and the
                    // other is genuine transparency.
                    float nearness = pow(saturate(1.0 - t), _TintFalloff);
                    float3 ringColor = lightColor * lerp(float3(1.0, 1.0, 1.0), _SelectedColor.rgb, nearness);

                    float ringAlpha = _BaseAlpha * pow(saturate(1.0 - t), _AlphaFalloff) * spatialCoverage;

                    accum.rgb = lerp(accum.rgb, ringColor, ringAlpha);
                    accum.a = ringAlpha + accum.a * (1.0 - ringAlpha);
                }

                return accum;
            }
            ENDCG
        }
    }
}
