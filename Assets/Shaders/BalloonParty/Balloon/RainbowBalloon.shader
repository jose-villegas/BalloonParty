Shader "BalloonParty/Balloon/RainbowBalloon"
{
    // A "rainbow star" balloon: the flat sprite tint is replaced by scrolling diagonal
    // colour bands cycling through up to four selectable colours. Colour count + values are
    // meant to be driven at runtime from the level's allowed colours (via MaterialPropertyBlock),
    // but default to the full palette so the material previews standalone. Keeps SpriteShineShadow's
    // diagonal shine sweep (no drop shadow) plus a scattered twinkling glitter layer on top. An
    // optional UV-rect mask excludes a region (e.g. the balloon's knot) from the band tint, so it
    // reads as a stable part of the sprite instead of being cut across by the scroll.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Bands)]
        _Color0 ("Colour 0", Color) = (0.467, 0.867, 0.467, 1)
        _Color1 ("Colour 1", Color) = (1.0, 0.412, 0.380, 1)
        _Color2 ("Colour 2", Color) = (0.012, 0.663, 0.957, 1)
        _Color3 ("Colour 3", Color) = (0.612, 0.153, 0.690, 1)
        _BandCount   ("Colour Count",  Range(1, 4))  = 4
        _StripeCount ("Stripe Density", Range(1, 12)) = 5
        _ScrollSpeed ("Scroll Speed",  Range(-5, 5)) = 1.0
        _BandBlend   ("Edge Softness", Range(0, 0.5)) = 0.08
        _BandAngle   ("Angle (turns)", Range(0, 1))  = 0.125
        // OPT-IN scene lighting: on, the colour bands scroll along _SceneLightDir instead of
        // the authored angle (scenario objects) — same polarity as the shine toggle below.
        [ToggleUI] _BandsFromSceneLight ("Bands Follow Scene Light", Float) = 0
        // Wavy colour seams: dual-sine displacement along the seam direction. 0 = straight.
        _SeamSwirlAmount ("Seam Swirl Amount", Range(0, 0.2)) = 0
        _SeamSwirlScale  ("Seam Swirl Scale",  Range(0, 60))  = 18
        _SeamSwirlSpeed  ("Seam Swirl Speed",  Range(0, 10))  = 1.5
        [HideInInspector] _TimeOffset ("Time Offset", Float) = 0

        [Header(Mask)]
        // UV-space rectangle excluded from the band tint (e.g. the knot). Zero-size = no mask.
        _MaskMin ("Mask Min (UV)", Vector) = (0, 0, 0, 0)
        _MaskMax ("Mask Max (UV)", Vector) = (0, 0, 0, 0)
        _MaskSoftness ("Mask Edge Softness", Range(0, 0.2)) = 0.02

        [Header(Shine)]
        _ShineWidth    ("Width",    Range(0, 1))   = 0.1
        _ShineSpeed    ("Speed",    Range(0, 5))   = 1.0
        _ShineInterval ("Interval", Range(0, 10))  = 3.0
        _ShineAngle    ("Angle (turns)", Range(0, 1)) = 0.125
        // OPT-IN scene lighting: on, the sweep axis derives from _SceneLightDir instead of the
        // authored angle (scenario objects); sweep timing untouched. The glitter binds to the
        // shine via _GlitterShineBind, so it follows automatically.
        [ToggleUI] _ShineFromSceneLight ("Shine Follows Scene Light", Float) = 0
        // Spherical deformation for the WHOLE banded look (colour bands + shine): bows the
        // pattern over the sphere's bulge so it reads as wrapping the balloon instead of
        // sliding flat across it. Fit the sphere to the sprite with Center/Radius (uv).
        _SphereBend   ("Sphere Bend", Range(-1, 1)) = 0
        _SphereCenter ("Sphere Center (uv)", Vector) = (0.5, 0.5, 0, 0)
        _SphereRadius ("Sphere Radius (uv)", Range(0.1, 0.8)) = 0.45
        // Diffuse response to the scene light (colour x intensity multiplies the composed body):
        // 0 = unlit (authored look always), 1 = fully lit. The bands keep their palette hue —
        // dial this down if colour-identity readability ever beats scene mood.
        _LightInfluence ("Light Influence", Range(0, 1)) = 1

        [Header(Glitter)]
        // Scattered twinkling specks on top of the shine sweep — a grid of pseudo-random dots, each
        // blinking at its own phase, jittered off-grid so it doesn't read as a rigid lattice.
        _GlitterDensity    ("Density (cells)",  Range(4, 64))  = 24
        _GlitterSize       ("Speck Size",       Range(0, 0.5)) = 0.16
        _GlitterChance     ("Speck Chance",     Range(0, 1))   = 0.35
        _GlitterSpeed      ("Twinkle Speed",    Range(0, 20))  = 6.0
        _GlitterSharpness  ("Twinkle Sharpness", Range(1, 32)) = 8.0
        _GlitterBrightness ("Brightness",       Range(0, 3))   = 1.0
        // 1 = glitter only twinkles along the shine band; 0 = glitter everywhere, independent of the shine.
        _GlitterShineBind  ("Bind to Shine",    Range(0, 1))   = 1.0

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 uv       : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float     _ShineWidth;
            float     _ShineSpeed;
            float     _ShineInterval;
            float     _ShineAngle;
            float     _ShineFromSceneLight;

            float     _GlitterDensity;
            float     _GlitterSize;
            float     _GlitterChance;
            float     _GlitterSpeed;
            float     _GlitterSharpness;
            float     _GlitterBrightness;
            float     _GlitterShineBind;

            fixed4 _Color0;
            fixed4 _Color1;
            fixed4 _Color2;
            fixed4 _Color3;
            float  _BandCount;
            float  _StripeCount;
            float  _ScrollSpeed;
            float  _BandBlend;
            float  _BandAngle;
            float  _BandsFromSceneLight;
            float  _SeamSwirlAmount;
            float  _SeamSwirlScale;
            float  _SeamSwirlSpeed;
            float  _SphereBend;
            float  _LightInfluence;
            float4 _SphereCenter;
            float  _SphereRadius;
            float  _TimeOffset;

            float2 _MaskMin;
            float2 _MaskMax;
            float  _MaskSoftness;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv     = IN.uv;
                // Carries the SpriteRenderer's per-frame alpha (spawn/despawn fades) but NOT a
                // palette tint — the bands provide the colour.
                OUT.color  = IN.color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            inline fixed3 ColorAt(int i)
            {
                if (i <= 0) { return _Color0.rgb; }
                if (i == 1) { return _Color1.rgb; }
                if (i == 2) { return _Color2.rgb; }
                return _Color3.rgb;
            }

            // Swirly seams: a dual-frequency sine along the seam direction (perpendicular to
            // the scroll axis) displaces the band coordinate, bending straight boundaries into
            // animated waves. Shared by the colour seams AND the shine so both wave together.
            // Amount 0 = off.
            inline float SeamSwirl(float across)
            {
                float wobbleT = _Time.y + _TimeOffset;
                return (sin(across * _SeamSwirlScale + wobbleT * _SeamSwirlSpeed)
                      + 0.5 * sin(across * _SeamSwirlScale * 2.7 - wobbleT * _SeamSwirlSpeed * 1.7))
                      * _SeamSwirlAmount;
            }

            // Spherical bulge shared by the colour bands and the shine: the fitted sphere's
            // height at this pixel, as a projection displacement. C1-smooth profile (zero slope
            // at rim AND apex) instead of a hemisphere's sqrt — the sqrt's infinite gradient at
            // the silhouette crushes the stripes into a visible seam at high bend; smoothstep
            // keeps the banding continuous everywhere. Fit via _SphereCenter/_SphereRadius.
            inline float SphereBulge(float2 uv)
            {
                float2 d = (uv - _SphereCenter.xy) / max(_SphereRadius, 1e-3);
                float z = smoothstep(0.0, 1.0, saturate(1.0 - dot(d, d)));
                return _SphereBend * z;
            }

            inline fixed3 RainbowBand(float2 uv, float2 worldPos)
            {
                // Opted-in: the bands scroll along the scene light's axis instead of the
                // authored angle. Same local-UV sway caveat as the shine.
                float ang = _BandAngle * 6.2831853;
                float2 axis = _BandsFromSceneLight > 0.5
                    ? SceneLightDirectionAt(worldPos)
                    : float2(cos(ang), sin(ang));
                float projection = dot(uv - 0.5, axis) + 0.5;
                float across = dot(uv - 0.5, float2(-axis.y, axis.x));
                float swirl = SeamSwirl(across);

                // The bands share the shine's spherical bulge, so the whole pattern wraps the
                // balloon coherently.
                float s = (projection + swirl + SphereBulge(uv)) * _StripeCount
                          + _Time.y * _ScrollSpeed + _TimeOffset;
                float cell = floor(s);
                float t = frac(s);

                float n = max(_BandCount, 1.0);
                float m = fmod(fmod(cell, n) + n, n); // always in [0, n), even for negative scroll
                int i0 = (int)m;
                int i1 = (int)fmod(m + 1.0, n);

                float edge = max(_BandBlend, 1e-4);
                float blend = smoothstep(1.0 - edge, 1.0, t);
                return lerp(ColorAt(i0), ColorAt(i1), blend);
            }

            // 1 inside [_MaskMin, _MaskMax] (band excluded there), 0 outside, soft edge in between.
            // Defaults to a zero-size rect, so the mask is off unless deliberately configured.
            inline float MaskAmount(float2 uv)
            {
                float2 lowerEdge = smoothstep(_MaskMin - _MaskSoftness, _MaskMin + _MaskSoftness, uv);
                float2 upperEdge = 1.0 - smoothstep(_MaskMax - _MaskSoftness, _MaskMax + _MaskSoftness, uv);
                float2 inside = lowerEdge * upperEdge;
                return inside.x * inside.y;
            }

            // Additive white shine sweep (0..1) — same diagonal band shape as SpriteShineShadow, angle tunable.
            inline fixed ShineAmount(float2 uv, float2 worldPos)
            {
                float sweepDuration = 1.0 / max(_ShineSpeed, 0.001);
                float cycleDuration = sweepDuration + _ShineInterval;
                float t = fmod(_Time.y, cycleDuration);
                float shineLocation = -_ShineWidth + (1.0 + 2.0 * _ShineWidth) * saturate(t / sweepDuration);

                // Opted-in: the sweep axis derives from the scene light instead of the authored
                // angle — travelling DOWN-light (enters from the lit side; top-to-bottom under
                // the canonical upper-left light). Caveat: uv is local sprite space (not
                // rotation-compensated), so the axis sways with the balloon — accepted.
                float shineAng = _ShineAngle * 6.2831853;
                float2 axis = _ShineFromSceneLight > 0.5
                    ? -SceneLightDirectionAt(worldPos)
                    : float2(cos(shineAng), sin(shineAng));
                float projection = dot(uv - 0.5, axis) + 0.5;

                // The glint waves with the colour seams (same SeamSwirl) and wraps the balloon
                // with the same spherical bulge as the bands — one coherent deformed pattern.
                float across = dot(uv - 0.5, float2(-axis.y, axis.x));
                projection += SeamSwirl(across) + SphereBulge(uv);
                float inside = step(shineLocation - _ShineWidth, projection) * step(projection, shineLocation + _ShineWidth);
                return inside * (1.0 - abs(projection - shineLocation) / _ShineWidth);
            }

            // Cheap deterministic 2D hash -> pseudo-random value in [0, 1). No texture lookup needed.
            inline float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Scattered twinkling specks: tile UV into a grid, jitter each speck off its cell centre,
            // only some cells sparkle at all, and each blinks at its own random phase/speed.
            inline fixed GlitterAmount(float2 uv)
            {
                float2 cellUv  = uv * _GlitterDensity;
                float2 cellId  = floor(cellUv);
                float2 cellPos = frac(cellUv) - 0.5;

                float2 jitter = float2(Hash21(cellId + 17.0), Hash21(cellId + 91.0)) - 0.5;
                float  dist   = length(cellPos - jitter * 0.6);
                float  speck  = smoothstep(_GlitterSize, 0.0, dist);

                float rnd     = Hash21(cellId);
                float phase   = rnd * 6.2831853;
                float twinkle = saturate(sin(_Time.y * _GlitterSpeed + phase) * 0.5 + 0.5);
                twinkle = pow(twinkle, max(_GlitterSharpness, 1.0));

                float active = step(1.0 - _GlitterChance, Hash21(cellId + 5.0));

                return speck * twinkle * active;
            }

            fixed4 frag(Varyings IN) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, IN.uv);

                // Masked region (e.g. the knot) keeps its plain sprite colour instead of the band tint.
                fixed3 bandColor = lerp(RainbowBand(IN.uv, IN.worldPos), fixed3(1, 1, 1), MaskAmount(IN.uv));

                // Sprite shading × band colour, then additive white shine on top. The glitter is bound to
                // the shine amount (by _GlitterShineBind) so the specks only twinkle along the sweeping band.
                fixed3 rgb = tex.rgb * bandColor * IN.color.rgb;

                // Diffuse term: the composed body is lit by the scene light. The bands keep their
                // palette HUE (no per-band swap) — brightness and cast follow the light, eased by
                // _LightInfluence. The shine/glitter emissives above their own gating stay additive.
                rgb *= lerp(float3(1.0, 1.0, 1.0), SceneLightTintAt(IN.worldPos), _LightInfluence);
                fixed shine = ShineAmount(IN.uv, IN.worldPos);
                // Opted-in shine is "lit by the scene light" — axis AND colour — so tint it;
                // the classic default sweep stays pure white regardless of the scene light.
                float3 shineTint = _ShineFromSceneLight > 0.5 ? SceneLightTintAt(IN.worldPos) : float3(1.0, 1.0, 1.0);
                rgb += tex.a * shine * shineTint;
                rgb += tex.a * GlitterAmount(IN.uv) * lerp(1.0, shine, _GlitterShineBind) * _GlitterBrightness;

                fixed4 result;
                result.rgb = rgb;
                result.a   = tex.a * IN.color.a;

                return result;
            }
            ENDCG
        }
    }
}
