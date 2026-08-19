Shader "BalloonParty/Display/SkyScatter"
{
    // The sky-scatter backdrop: renders on the quad SkyScatterService builds at startup, sorted
    // behind every other backdrop layer (BackgroundCloud, WallNet, SmokeField) so it reads as
    // replacing the camera's flat clear colour rather than sitting on top of it. A naive 2D stand-in
    // for atmospheric scattering: a stack of circles that all share one tangent point toward the
    // scene light, growing outward from the light's own colour to an authored horizon colour, with a
    // slow per-ring wave so the stack breathes instead of sitting static.
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
        _HorizonColor     ("Horizon Color",    Color)              = (0.55, 0.75, 0.95, 1)
        _ColorCurve       ("Color Curve",      Range(0.2, 3))      = 1

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
        Blend Off

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

            fixed4 _HorizonColor;
            float  _ColorCurve;
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
                float3 color = _HorizonColor.rgb;
                float3 lightColor = SceneLightTint();

                // Back-to-front: the biggest ring (horizon colour) paints first, each smaller ring
                // layers on top. Every ring shares the same anchor tangent point (its centre sits
                // exactly one radius from the anchor), so growing radii nest automatically — no
                // per-ring containment test needed.
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
                    float coverage = smoothstep(radius + _EdgeSoftness, radius - _EdgeSoftness, dist);

                    float3 ringColor = lerp(lightColor, _HorizonColor.rgb, pow(t, _ColorCurve));
                    color = lerp(color, ringColor, coverage);
                }

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
