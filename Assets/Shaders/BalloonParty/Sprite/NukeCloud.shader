Shader "BalloonParty/Sprite/NukeCloud"
{
    // The nuke's mushroom cloud (PLAN-NukeBalloon), seen from straight above as the board is. NukeCloudView draws it
    // on one square quad centred where the nuke went off and pushes its progress, 0 at the blast to 1 when it is
    // gone. It is shapes, not a texture: the cap is a height field, a dome swelling over the blast with three layers
    // of billows piled on it from the tileable noise, big to small, the biggest flowing outward from the middle as
    // the cap's top rolls over its rim. Its edge is where the heaped puffs run out, so it is ragged with them. Each
    // puff is lit by its own slope toward the scene light, shadowed by the taller ones between it and the light (a
    // few steps marched along the height field) and darker in the folds between puffs, so the smoke reads as
    // layered lumps, not a flat sheet. The cap casts its shadow on the board away from the light; under it a ring of
    // dust thrown along the ground races out past the cap and thins. At first the cap is fire, white-hot in the
    // middle and orange at the edge, cooling to smoke as the progress runs; the smoke takes the scene light (so night
    // leaves it dark). It is smoke, not a solid: only its thickest heaps hide the board, its thin edges and the folds
    // between puffs let it show through, and light glows through the thin parts. Over its last stretch it thins,
    // spreads a little and erodes away through the noise, edges first, until nothing is left.

    Properties
    {
        [Header(Shape)]
        // World units: the cap's radius once it has spread, and how far out the dust ring runs.
        _CapRadius   ("Cap Radius", Range(0.5, 6)) = 2.7
        _RingRadius  ("Dust Ring Radius", Range(0.5, 10)) = 4.6
        _RingWidth   ("Dust Ring Width", Range(0.05, 3)) = 0.7
        // How far the noise pushes the edges in and out, in shares of each shape's size.
        _Billow      ("Billow", Range(0, 1)) = 0.28
        // How far the cap's shadow falls from it, away from the light, in world units.
        _ShadowOffset("Shadow Offset", Range(0, 3)) = 0.7
        _ShadowAlpha ("Shadow Strength", Range(0, 1)) = 0.45

        [Header(Motion)]
        [NoScaleOffset] _NoiseTex ("Noise (tileable)", 2D) = "grey" {}
        _NoiseScale  ("Noise Scale", Range(0.02, 2)) = 0.22
        // How fast the billows flow from the cap's middle out to its rim, and how fast the cap turns.
        _RollSpeed   ("Roll Speed", Range(0, 2)) = 0.35
        _TurnSpeed   ("Turn Speed", Range(0, 1)) = 0.06

        [Header(Fire)]
        _FireHot     ("Hot", Color) = (1.00, 0.95, 0.75, 1)
        _FireCool    ("Cool", Color) = (1.00, 0.36, 0.05, 1)
        // Past white so the fire blooms (PLAN-HDR).
        _FireBoost   ("HDR Boost", Range(1, 8)) = 3
        // The progress by which the fire has burned down to smoke.
        _FireGone    ("Gone By Progress", Range(0.1, 1)) = 0.5

        [Header(Smoke)]
        _SmokeLit    ("Lit", Color) = (0.96, 0.90, 0.82, 1)
        _SmokeDark   ("Shadowed", Color) = (0.30, 0.25, 0.24, 1)
        _SmokeAlpha  ("Strength", Range(0, 1)) = 0.88
        // How much of the board the thinnest smoke still hides, as a share of the thickest's.
        _Thinnest    ("Thinnest", Range(0, 1)) = 0.2
        // How much light glows through the thin smoke.
        _Glow        ("Glow Through", Range(0, 1)) = 0.35
        _Dust        ("Dust", Color) = (0.78, 0.68, 0.55, 1)
        _DustAlpha   ("Dust Strength", Range(0, 1)) = 0.6
        // How steep the puffs' slopes light, how far the taller puffs shadow the ones behind them, and how dark the
        // folds between puffs are.
        _PuffContrast("Puff Contrast", Range(0, 6)) = 3.5
        _SelfShadow  ("Self Shadow", Range(0, 1)) = 0.75
        _Folds       ("Fold Darkening", Range(0, 1)) = 0.5
        // How high the light stands over the board: low rakes across the puffs, high lights them flat.
        _SunHeight   ("Sun Height", Range(0.1, 2)) = 0.7

        [Header(Fade)]
        // The progress from which the cloud erodes away.
        _FadeFrom    ("Fade From", Range(0, 1)) = 0.45

        [PerRendererData] _Progress ("Progress", Float) = 0
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // The fragment's offset from the blast, in world units, and its world position for the scene light.
                float2 offset : TEXCOORD0;
                float2 world  : TEXCOORD1;
            };

            float _CapRadius;
            float _RingRadius;
            float _RingWidth;
            float _Billow;
            float _ShadowOffset;
            float _ShadowAlpha;
            sampler2D _NoiseTex;
            float _NoiseScale;
            float _RollSpeed;
            float _TurnSpeed;
            fixed4 _FireHot;
            fixed4 _FireCool;
            float _FireBoost;
            float _FireGone;
            fixed4 _SmokeLit;
            fixed4 _SmokeDark;
            float _SmokeAlpha;
            float _Thinnest;
            float _Glow;
            fixed4 _Dust;
            float _DustAlpha;
            float _PuffContrast;
            float _SelfShadow;
            float _Folds;
            float _SunHeight;
            float _FadeFrom;
            float _Progress;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                float2 world = mul(unity_ObjectToWorld, IN.vertex).xy;
                OUT.world = world;
                OUT.offset = world - mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                return OUT;
            }

            // Two octaves of the tileable noise, -1 to 1.
            float Billow(float2 p)
            {
                float a = tex2D(_NoiseTex, p * _NoiseScale).r;
                float b = tex2D(_NoiseTex, p * _NoiseScale * 2.7 + 0.37).r;
                return (a * 0.75 + b * 0.25) * 2.0 - 1.0;
            }

            float2 Rotate(float2 p, float angle)
            {
                float s = sin(angle);
                float c = cos(angle);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            // Where the cap's surface pattern is read for a point: turned slowly, and pulled toward the middle as time
            // runs so the billows drift outward over the rim (the top of the mushroom rolling over its edge). The pull
            // wraps every so often, cross-faded by the caller, so the flow never stretches the pattern for long.
            float2 CapFrame(float2 p, float phase)
            {
                return Rotate(p, _Time.y * _TurnSpeed) * exp2(-frac(phase)) + floor(phase) * 3.17;
            }

            float CapNoise(float2 p)
            {
                float phase = _Time.y * _RollSpeed;
                float w = frac(phase);
                // Two flows half a cycle apart, weighted so each fades out as it wraps.
                float a = Billow(CapFrame(p, phase));
                float b = Billow(CapFrame(p, phase + 0.5));
                float wa = 1.0 - abs(2.0 * w - 1.0);
                return lerp(b, a, wa);
            }

            // A rounded lump from noise, 0 to 1: the noise's high ground swollen into puffs.
            float Puff(float n)
            {
                float b = saturate(n * 0.5 + 0.5);
                return b * b * (3.0 - 2.0 * b);
            }

            // The cap's height at p: the dome, then the big flowing billows, then two smaller layers turned their own
            // ways. Near 0 at the rim, so the edge is where the heaped puffs run out.
            float CapHeight(float2 p, float capR, float big)
            {
                // Past the rim the dome goes negative, so no puff rises out there.
                float rr = min(dot(p, p) / (capR * capR), 2.0);
                float dome = 1.0 - rr;
                float mid = Puff(Billow(Rotate(p, -_Time.y * _TurnSpeed * 1.7) * 1.1 + 1.9));
                float small = Puff(Billow(Rotate(p, _Time.y * _TurnSpeed * 2.6) * 2.2 + 7.3));
                float piles = Puff(big) * 0.55 + mid * 0.3 + small * 0.15;
                return dome * 0.75 + (piles - 0.5) * (0.35 + 0.35 * sqrt(saturate(dome))) - 0.04 * saturate(1.0 - dome);
            }

            // The big billows alone, for the steps marched toward the light: the small layers barely shadow.
            float CapHeightCoarse(float2 p, float capR)
            {
                // Past the rim the dome goes negative, so no puff rises out there.
                float rr = min(dot(p, p) / (capR * capR), 2.0);
                float dome = 1.0 - rr;
                return dome * 0.75 + (Puff(Billow(CapFrame(p, _Time.y * _RollSpeed))) * 0.55 - 0.3) * (0.35 + 0.35 * sqrt(saturate(dome)));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float t = saturate(_Progress);
                // Fast at first, slowing as it tops out.
                float rise = 1.0 - pow(1.0 - t, 3.0);
                float2 p = IN.offset;
                float r = length(p);
                float2 toLight = SceneLightDirection();

                // The cap: the height field, its edge where the heaped puffs run out. Its slope is read from the
                // screen-space derivatives, so it costs nothing more (taken here, before any branch).
                float fade = smoothstep(_FadeFrom, 1.0, t);
                // Still spreading a little as it thins away.
                float capR = _CapRadius * (0.2 + 0.8 * rise) * (1.0 + 0.2 * fade);
                float noise = CapNoise(p);
                float height = CapHeight(p, capR, noise);
                float2 slope2 = float2(ddx(height), ddy(height)) / max(float2(abs(ddx(p.x)), abs(ddy(p.y))), 1e-5);
                float capD = -height / max(_Billow, 0.05);

                // Its shadow: the same disc moved away from the light, read at the point moved toward it.
                float2 sp = p - toLight * _ShadowOffset * rise;
                float shadowD = length(sp) / capR - 1.0 + _Billow * CapNoise(sp);

                // The dust ring: a band racing out along the ground ahead of the cap, early, and thinning as it goes.
                float spread = 1.0 - pow(1.0 - saturate(t * 1.6), 2.0);
                float ringR = _RingRadius * spread;
                float ringNoise = Billow(Rotate(p, -_Time.y * 0.1) * 1.4 + 5.3);
                float ringD = abs(r - ringR) / (_RingWidth * (0.4 + 0.6 * spread)) - 1.0 + 0.6 * ringNoise;
                float dust = smoothstep(0.1, -0.5, ringD) * saturate(1.0 - spread * 0.85) * saturate(r / max(capR, 1e-3) - 0.6);

                // Erodes over the fade: the inside threshold climbs through the noise, edges first, so holes open
                // and spread.
                // Holes open through the noise, gently: the thinning below is what takes it away, all of it by the
                // end, so nothing is left to vanish at once.
                float erode = fade * (1.2 * fade + 0.6 * Billow(p * 2.3 + _Time.y * 0.15) + 0.5 * saturate(r / capR));
                float density = smoothstep(0.02, -0.3, capD + erode);
                float shadow = smoothstep(0.1, -0.3, shadowD + erode) * (1.0 - density) * _ShadowAlpha * (1.0 - fade);
                dust *= 1.0 - fade;

                // Each puff lit by its own slope toward the light, in the shadow of taller puffs between it and the
                // light, and darker in the folds below the puffs around it.
                float3 sun = normalize(float3(toLight, _SunHeight));
                float3 normal = normalize(float3(-slope2 * capR * 0.05 * _PuffContrast, 1.0));
                float lit = saturate(dot(normal, sun));
                float blocked = 0.0;
                float step = capR * 0.09;
                float rise3 = _SunHeight / length(toLight + 1e-4) * step / capR;
                [unroll]
                for (int i = 1; i <= 3; i++)
                {
                    float ahead = CapHeightCoarse(p + toLight * step * i, capR);
                    blocked = max(blocked, saturate((ahead - height - rise3 * i) * 8.0));
                }

                float folds = saturate(1.0 - (CapHeightCoarse(p, capR) - height) * 4.0);
                float shade = lit * (1.0 - _SelfShadow * blocked) * 0.8 + 0.3 * lerp(1.0, folds, _Folds);
                float3 light = SceneLightTint() * SceneLightMagnitudeAt(IN.world);
                float3 smoke = lerp(_SmokeDark.rgb, _SmokeLit.rgb, saturate(shade)) * light;
                // Thick in the heaps, thin at the edges and in the folds, and thinner still as it fades; light
                // glows through where it is thin.
                float thickness = saturate(height / 0.7) * lerp(0.55, 1.0, folds);
                smoke = lerp(smoke, _SmokeLit.rgb * light, (1.0 - thickness) * _Glow);

                // Fire: hottest in the middle at the start, cooling outward first, gone by _FireGone.
                float burn = saturate(1.0 - t / _FireGone);
                float core = saturate(1.0 - r / capR);
                float heat = saturate(burn * (core * 1.4 + 0.25) + 0.15 * noise * burn) * density;
                float3 fire = lerp(_FireCool.rgb, _FireHot.rgb, saturate(core * burn * 1.6)) * _FireBoost;

                // Premultiplied: the shadow only darkens what is under it; dust, smoke and fire cover it.
                float smokeAlpha = density * _SmokeAlpha * lerp(_Thinnest, 1.0, max(thickness, saturate(heat)))
                                 * pow(1.0 - fade, 1.3);
                float dustAlpha = dust * _DustAlpha * (1.0 - smokeAlpha);
                float3 colour = lerp(smoke, fire, saturate(heat)) * smokeAlpha + _Dust.rgb * light * dustAlpha;
                float alpha = saturate(smokeAlpha + dustAlpha + shadow);
                if (alpha <= 0.002)
                {
                    discard;
                }

                return fixed4(colour, alpha);
            }
            ENDCG
        }
    }
}
