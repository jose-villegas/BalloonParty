Shader "BalloonParty/Scenario/SpeckField"
{
    // Renders the GPU speck buffer as camera-facing quads (one per speck), expanded in the vertex shader
    // from SV_VertexID — no per-frame CPU. SpeckField.cs draws it through a MeshRenderer holding a dummy
    // count*6-vertex mesh (so the field sorts with sprites); each vert is repositioned here from _Specks.
    Properties
    {
        _MainTex   ("Speck Sprite", 2D)              = "white" {}
        _Color     ("Tint",         Color)           = (1, 1, 1, 0.5)
        // The untagged-disturbance flush; alpha = disturbed opacity. Palette-tagged specks use their
        // palette color (and its alpha) instead.
        _DisturbColor ("Disturbed Tint", Color)       = (1, 0.65, 0.25, 1)
        _SpeckSize ("Speck Size",   Float)           = 0.03
        _MinScale  ("Min Scale",    Float)           = 0.5
        _MaxScale  ("Max Scale",    Float)           = 1.5
        _ScalePulses ("Scale Pulses Over Life (min,max)", Vector) = (0.4, 1.0, 0, 0)
        _ScaleHold ("Scale Hold At Full (min,max frac)", Vector) = (0, 0, 0, 0)
        _TrailLength ("Trail Length (per speed)", Float) = 0
        _TrailMax    ("Trail Max Length", Float)         = 0.5
        _FadeIn    ("Fade In (life frac)",  Range(0, 1)) = 0.15
        _FadeOut   ("Fade Out (life frac)", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct Speck
            {
                float2 position;
                float2 velocity;
                float seed;
                float age;
                float lifetime;
                float2 effectiveVel;
                float heat;
                float paletteIndex;
                float prevPaletteIndex;
                float colorBlend;
            };

            StructuredBuffer<Speck> _Specks;
            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _DisturbColor;
            float4 _SpeckPalette[16];
            int _SpeckPaletteCount;
            float _SpeckSize;
            float _MinScale;
            float _MaxScale;
            float4 _ScalePulses;
            float4 _ScaleHold;
            float _FadeIn;
            float _FadeOut;
            float _TrailLength;
            float _TrailMax;
            int _ActiveCount;

            // Per-palette-colour look overrides a speck blends toward by its heat (slot = paletteIndex).
            // A=(size, trailLength, trailMax, fadeIn), B=(fadeOut, minScale, maxScale, pulseMin), C=(pulseMax…).
            float4 _SpeckLookA[16];
            float4 _SpeckLookB[16];
            float4 _SpeckLookC[16];
            float4 _SpeckLookD[16];   // D = (lightInfluence, lightMode, _, _) per slot
            int _SpeckLookCount;

            // Base scene-light response (uncoloured specks); a colour look overrides via _SpeckLookD.
            float _SpeckLightInfluence;
            float _SpeckLightMode;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float  alpha : TEXCOORD1;
                float  heat  : TEXCOORD2;
                float  paletteIndex : TEXCOORD3;
                float  prevPaletteIndex : TEXCOORD4;
                float  colorBlend : TEXCOORD5;
                float3 lightTint : TEXCOORD6;
                float  lightInfluence : TEXCOORD7;
            };

            // Two triangles → a unit quad centered on the origin, in [-0.5, 0.5].
            static const float2 Corners[6] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
                float2(-0.5, -0.5), float2(0.5, 0.5), float2(-0.5, 0.5)
            };

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            v2f vert(uint vid : SV_VertexID)
            {
                // The mesh has 6 verts per speck; split the id into speck index + quad corner.
                uint speckIndex = vid / 6u;

                // Specks past the enabled count collapse offscreen — the field builds up as pops enable more.
                if (speckIndex >= (uint)_ActiveCount)
                {
                    v2f dead = (v2f)0;
                    dead.pos = float4(2.0, 2.0, 2.0, 1.0); // outside clip space → culled
                    return dead;
                }

                Speck s = _Specks[speckIndex];
                float2 corner = Corners[vid % 6u];

                // Blend the render look from the base toward this colour's profile by the speck's heat, so a
                // coloured speck also takes on that colour's look. paletteIndex < 0 (uncoloured) stays base;
                // uncovered colours resolve to base values, so their lerp is a no-op.
                int lookSlot = clamp((int)(s.paletteIndex + 0.5), 0, 15);
                float lookT = (s.paletteIndex >= -0.5 && lookSlot < _SpeckLookCount) ? saturate(s.heat) : 0.0;
                float4 la = _SpeckLookA[lookSlot];
                float4 lb = _SpeckLookB[lookSlot];
                float4 lc = _SpeckLookC[lookSlot];
                float lSize        = lerp(_SpeckSize,          la.x, lookT);
                float lTrailLength = lerp(_TrailLength,        la.y, lookT);
                float lTrailMax    = lerp(_TrailMax,           la.z, lookT);
                float lFadeIn      = lerp(_FadeIn,             la.w, lookT);
                float lFadeOut     = lerp(_FadeOut,            lb.x, lookT);
                float lMinScale    = lerp(_MinScale,           lb.y, lookT);
                float lMaxScale    = lerp(_MaxScale,           lb.z, lookT);
                float lPulsesMin   = lerp(_ScalePulses.x,      lb.w, lookT);
                float lPulsesMax   = lerp(_ScalePulses.y,      lc.x, lookT);
                float lHoldMin     = lerp(_ScaleHold.x,        lc.y, lookT);
                float lHoldMax     = lerp(_ScaleHold.y,        lc.z, lookT);

                // Scene-light response, sampled at the speck's world position. Influence blends base →
                // colour by heat (like the params above); mode is discrete — a coloured speck reads its
                // colour slot's mode, an uncoloured one the base. Local mode is neutral at rest and only
                // brightens near a local light. Mode: 0 = Full, 1 = Ambient, 2 = Local.
                bool coloured = s.paletteIndex >= -0.5 && lookSlot < _SpeckLookCount;
                float lMode = coloured ? _SpeckLookD[lookSlot].y : _SpeckLightMode;
                float3 lightTint = lMode > 1.5
                    ? float3(1.0, 1.0, 1.0) + SceneLightLocalAtLOD(s.position)
                    : (lMode > 0.5 ? SceneLightTint() : SceneLightTintAtLOD(s.position));
                float lightInfluence = lerp(_SpeckLightInfluence, _SpeckLookD[lookSlot].x, lookT);

                // Life fade: ramp in over fadeIn, out over fadeOut (fractions of lifetime), driving both
                // alpha and scale so specks pop in and shrink away.
                float lifeT = s.lifetime > 0.0 ? saturate(s.age / s.lifetime) : 1.0;
                float fadeIn = lFadeIn > 0.0 ? saturate(lifeT / lFadeIn) : 1.0;
                float fadeOut = lFadeOut > 0.0 ? saturate((1.0 - lifeT) / lFadeOut) : 1.0;
                float fade = fadeIn * fadeOut;

                // Scale pulse over the speck's OWN life (age/lifetime), not global time — so nudging the rate
                // via the per-colour heat blend can't swing a huge phase (the old sin(bigTime*speed) flicker),
                // and 0 pulses is genuinely static. Rate = number of pulses across the lifetime, per-speck
                // random; a per-speck phase offset keeps them desynced.
                float pulses = lerp(lPulsesMin, lPulsesMax, hash11(s.seed * 91.7));
                float scaleT;
                if (pulses <= 1e-4)
                {
                    // No pulse: a fixed random size per speck — size variety without any animation.
                    scaleT = hash11(s.seed * 12.3);
                }
                else
                {
                    // Each cycle holds at full scale for `hold` of its span, then a cosine dip to min and back.
                    float hold = saturate(lerp(lHoldMin, lHoldMax, hash11(s.seed * 41.7)));
                    float u = frac(lifeT * pulses + hash11(s.seed * 7.1));
                    scaleT = u < hold ? 1.0 : 0.5 + 0.5 * cos(6.2831853 * (u - hold) / max(1.0 - hold, 1e-4));
                }
                float baseScale = lerp(lMinScale, lMaxScale, scaleT);
                float size = lSize * baseScale * fade;

                // Trail: stretch the quad along the speck's motion, scaled by speed (capped by trailMax).
                // Below a tiny speed it stays a round dot (velocity direction is undefined there).
                float speed = length(s.effectiveVel);
                float2 along = float2(0.0, 1.0);
                float length2 = size;
                if (speed > 1e-4 && lTrailLength > 0.0)
                {
                    along = s.effectiveVel / speed;
                    length2 = size + min(speed * lTrailLength, lTrailMax);
                }
                float2 across = float2(along.y, -along.x);

                // 2D game on the XY plane — offset in the velocity-aligned frame (round when not trailing).
                float2 offset = across * (corner.x * size) + along * (corner.y * length2);
                float3 world = float3(s.position + offset, 0.0);

                v2f o;
                o.pos = UnityWorldToClipPos(world);
                o.uv = corner + 0.5;
                o.alpha = fade;
                o.heat = s.heat;
                o.paletteIndex = s.paletteIndex;
                o.prevPaletteIndex = s.prevPaletteIndex;
                o.colorBlend = s.colorBlend;
                o.lightTint = lightTint;
                o.lightInfluence = lightInfluence;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = tex * _Color;

                // Disturbed specks lerp toward the disturber's palette color (adopted from the field's A
                // channel), or the flat disturbed tint when untagged — color AND opacity — by their heat,
                // easing back as it cools.
                int paletteIndex = (int)(i.paletteIndex + 0.5);
                float4 target = _DisturbColor;
                if (i.paletteIndex >= -0.5 && paletteIndex < _SpeckPaletteCount)
                {
                    float4 cur = _SpeckPalette[paletteIndex];
                    int prevIndex = (int)(i.prevPaletteIndex + 0.5);
                    float4 prev = (i.prevPaletteIndex >= -0.5 && prevIndex < _SpeckPaletteCount)
                        ? _SpeckPalette[prevIndex] : cur;
                    // Crossfade the palette hop so a tag change (rainbow cycling) eases instead of snapping.
                    target = lerp(prev, cur, saturate(i.colorBlend));
                }

                float heat = saturate(i.heat);
                col.rgb = lerp(col.rgb, target.rgb, heat);
                col.a = lerp(col.a, tex.a * target.a, heat);

                // Scene-light tint as an exposure curve: influence=0 → emissive (tint ignored),
                // influence=1 → linear, >1 → stronger contrast. pow never goes negative, so even
                // a weak local light in a dark ambient is visible instead of clamped to black.
                col.rgb *= pow(max(float3(0.001, 0.001, 0.001), i.lightTint), i.lightInfluence);

                col.a *= i.alpha;
                return col;
            }
            ENDCG
        }
    }
}
