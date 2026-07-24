Shader "BalloonParty/Paint/PaintDrip"
{
    // Overlay played on a balloon-shaped child sprite when the Paint item hits. Draws the incoming
    // paint colour as a wavy, wet sheet that drains downward and runs off the silhouette, revealing
    // whatever is underneath. It never knows accept vs reject — the balloon body has (accept) or has
    // not (reject) already committed the new colour, so the same drip reads as paint settling or
    // sliding off. Driven per-instance by _Progress / _PaintColor / _Seed (MaterialPropertyBlock).
    Properties
    {
        [PerRendererData] _MainTex ("Sprite (silhouette)", 2D) = "white" {}
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [HideInInspector] _PaintColor ("Paint Color", Color) = (1, 0.2, 0.2, 1)
        [HideInInspector] _Progress   ("Progress",    Range(0, 1)) = 0
        [HideInInspector] _Seed       ("Seed",        Float)       = 0

        [Header(Fill)]
        // The settled paint reads a touch deeper than the body so a same-colour reveal is still visible.
        _FillShade ("Fill Shade", Range(0.4, 1.0)) = 0.85

        [Header(Drip Front)]
        _EdgeSoft  ("Front Softness", Range(0.005, 0.3)) = 0.08
        _WaveAmp   ("Wave Amount",    Range(0, 0.4))     = 0.12
        _WaveFreq  ("Wave Frequency", Range(1, 20))      = 7.0
        _WaveSpeed ("Wave Speed",     Range(0, 8))       = 2.5

        [Header(Wet Edge)]
        _WetColor ("Wet Tint",  Color)            = (1, 1, 1, 1)
        _WetWidth ("Wet Width", Range(0.01, 0.5)) = 0.14
        _WetGloss ("Wet Gloss", Range(0.5, 8))    = 2.5

        [Header(Surface Ripple)]
        // Perturbs a fake surface normal across the paint so the wet sheet undulates and catches the
        // scene light — the "it's liquid" cue. Lit via the shared scene-light direction.
        _RippleAmp      ("Ripple Bump",     Range(0, 5))    = 1.4
        _RippleFreq     ("Ripple Frequency", Range(1, 40))  = 14.0
        _RippleSpeed    ("Ripple Speed",    Range(0, 6))    = 1.0
        _RippleContrast ("Ripple Contrast", Range(0, 2))    = 0.6
        _SurfaceGloss   ("Surface Gloss",   Range(1, 64))   = 16.0
        _SurfaceSpec    ("Surface Spec",    Range(0, 2))    = 0.7
        _LightZ         ("Light Elevation", Range(0.1, 2))  = 0.6

        [Header(Falling Drops)]
        // A few beads that ride ON the wet surface, sliding down to the bottom edge as the sheet drains.
        // Clipped to the balloon silhouette, so no extra geometry is needed — just the body sprite.
        _DropCount   ("Drop Count",   Range(0, 5))         = 3
        _DropSize    ("Drop Size",    Range(0.005, 0.15))  = 0.05
        _DropStretch ("Drop Stretch (teardrop)", Range(1, 6)) = 2.5
        _DropStartY  ("Drop Start Y (uv)", Range(0, 1))    = 0.85
        _DropFall    ("Drop Fall Distance", Range(0, 1))   = 0.8
        _DropEndScale ("Drop End Scale", Range(0.1, 1))    = 0.4
        _DropShine   ("Drop Shine",  Range(0, 4))          = 1.6
        _DropGloss   ("Drop Gloss",  Range(1, 64))         = 24.0
        _DropSpread  ("Drop Spread X (min,max)", Vector)   = (0.25, 0.75, 0, 0)
        _DropStagger ("Drop Release Stagger", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
            "PreviewType"     = "Plane"
            "CanUseSpriteAtlas" = "True"
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
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

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
            };

            sampler2D _MainTex;
            fixed4 _RendererColor;
            fixed4 _PaintColor;
            float  _Progress;
            float  _Seed;
            float  _FillShade;
            float  _EdgeSoft;
            float  _WaveAmp;
            float  _WaveFreq;
            float  _WaveSpeed;
            fixed4 _WetColor;
            float  _WetWidth;
            float  _WetGloss;
            float  _RippleAmp;
            float  _RippleFreq;
            float  _RippleSpeed;
            float  _RippleContrast;
            float  _SurfaceGloss;
            float  _SurfaceSpec;
            float  _LightZ;
            float  _DropCount;
            float  _DropSize;
            float  _DropStretch;
            float  _DropStartY;
            float  _DropFall;
            float  _DropEndScale;
            float  _DropShine;
            float  _DropGloss;
            float4 _DropSpread;
            float  _DropStagger;

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // 2-octave value noise — cheap procedural (no texture fetch) for the drip's wavy front.
            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // 2-octave rolling height for the wet surface, animated so the ripples drift.
            float SurfaceHeight(float2 p)
            {
                return ValueNoise(p) + 0.5 * ValueNoise(p * 2.13 + 7.7);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _RendererColor;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.texcoord);

                // v: 1 at top, 0 at bottom. Paint drains DOWN (toward v=0) as progress climbs.
                float v = IN.texcoord.y;

                // Wavy front: value-noise + a travelling sine along u, seeded per drip. Displacing the
                // sample coordinate bends the straight drain line into running tendrils.
                float wobble = (ValueNoise(float2(IN.texcoord.x * _WaveFreq, _Seed)) - 0.5) * 2.0;
                float ripple = sin(IN.texcoord.x * _WaveFreq * UNITY_TWO_PI
                                   + _Time.y * _WaveSpeed + _Seed);
                float vv = v + (wobble + 0.5 * ripple) * _WaveAmp;

                // Front descends from above the top (fully covered) to below the bottom (fully gone).
                float front = lerp(1.0 + _EdgeSoft, -_EdgeSoft, _Progress);
                float cover = 1.0 - smoothstep(front - _EdgeSoft, front + _EdgeSoft, vv);

                // Bright wet band riding the draining front — the readable "it's wet" cue, even when the
                // paint colour matches the body underneath.
                float band = saturate(1.0 - abs(vv - front) / max(_WetWidth, 1e-3));
                float wet  = pow(band, _WetGloss);

                // Fake surface normal from an animated height field, finite-differenced. Gives the sheet
                // an undulating, light-catching wetness across its whole area (not just the front).
                float2 rp = IN.texcoord * _RippleFreq + float2(0.0, _Time.y * _RippleSpeed) + _Seed;
                const float e = 0.6;
                float h0 = SurfaceHeight(rp);
                float2 slope = float2(h0 - SurfaceHeight(rp + float2(e, 0.0)),
                                      h0 - SurfaceHeight(rp + float2(0.0, e))) / e;
                float3 N = normalize(float3(slope * _RippleAmp, 1.0));

                // Lit by the shared scene light (flat main direction, given an elevation so it isn't
                // grazing). Diffuse undulates brightness; a half-vector spec adds glints on the ripples.
                float3 L = normalize(float3(SceneLightDirection(), _LightZ));
                float ndl = saturate(dot(N, L));
                float spec = pow(saturate(dot(N, normalize(L + float3(0.0, 0.0, 1.0)))), _SurfaceGloss);
                float lit = 1.0 + (ndl - 0.5) * _RippleContrast;

                fixed3 rgb = _PaintColor.rgb * _FillShade * lit
                           + _WetColor.rgb * (wet + spec * _SurfaceSpec);
                fixed  a   = cover * tex.a * _PaintColor.a * IN.color.a;

                // Beads that ride ON the wet surface: a few teardrops that release during the run-off and
                // slide down to the bottom edge. Clipped to the balloon silhouette and layered over the
                // sheet — no extra geometry, and they keep sliding after the sheet behind them has drained.
                float  beadCov = 0.0;
                float2 beadN   = float2(0.0, 0.0); // winning bead's normalized offset, for its rounded normal
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    float fi     = (float)i;
                    float active = step(fi + 0.5, _DropCount);
                    float rx     = Hash21(float2(_Seed + fi * 1.37, 3.1));
                    float phase  = Hash21(float2(_Seed + fi * 2.71, 7.7)) * _DropStagger;
                    float dp     = saturate((_Progress - phase) / max(1.0 - phase, 1e-3));

                    // Beads shrink as they run down — the trail thins out toward the tail.
                    float size  = _DropSize * lerp(1.0, _DropEndScale, dp);
                    float dropX = lerp(_DropSpread.x, _DropSpread.y, rx);
                    float dropY = _DropStartY - dp * _DropFall;
                    float2 q    = IN.texcoord - float2(dropX, dropY);
                    q.y        /= max(_DropStretch, 1e-3);
                    float2 nrm  = q / max(size, 1e-4);

                    float blob = smoothstep(size, size * 0.4, length(q));
                    float fade = smoothstep(0.0, 0.15, dp) * (1.0 - smoothstep(0.8, 1.0, dp));
                    float cov  = blob * fade * active;

                    if (cov > beadCov)
                    {
                        beadCov = cov;
                        beadN   = nrm;
                    }
                }

                // Round-bead normal (sphere impostor) → a real specular glint that catches the scene
                // light, so each droplet reads as raised and wet even over same-colour paint.
                float  bz = sqrt(saturate(1.0 - saturate(dot(beadN, beadN))));
                float3 Nb = normalize(float3(beadN, bz + 0.15));
                float  beadSpec = pow(saturate(dot(Nb, normalize(L + float3(0.0, 0.0, 1.0)))), _DropGloss);

                fixed3 dropRgb = _PaintColor.rgb * _FillShade + _WetColor.rgb * beadSpec * _DropShine;

                float onSurface = beadCov * tex.a;
                fixed3 outRgb = lerp(rgb, dropRgb, onSurface);
                fixed  outA   = max(a, onSurface * _PaintColor.a * IN.color.a);

                return fixed4(outRgb, outA);
            }
            ENDCG
        }
    }
}
