// The PIERCING-state aura: a conical helix drilling forward around the shot, faked in pure UV
// math on whatever sprite quad carries the material (bullet_blur today, anything later — the
// sprite only contributes an optional alpha mask; the whole effect is procedural).
//
// The 3D read comes from two tricks, matching the classic cone-spiral drawing:
//  - DEPTH from phase: sin(θ) > 0 is the near side of each wind — drawn thicker and brighter;
//    the far side thins and dims (front/back thickness + dim knobs).
//  - ELLIPSES from tilt: each wind's projected height is pushed down/up by sin(θ) x the local
//    cone radius, so circles render as the slightly-viewed-from-above ellipses of the reference,
//    solved per-pixel with a short fixed-point iteration (the tilt is small, it converges fast).
//
// Orientation: V runs base(0) → apex(1); the projectile's transform.up is its heading, so the
// apex ends up at the nose and positive Spiral Speed climbs the strands toward it — drilling.
Shader "BalloonParty/Display/PierceConeSpiral"
{
    Properties
    {
        _MainTex("Sprite (alpha mask)", 2D) = "white" {}
        [HDR] _Color("Strand Color", Color) = (1, 1, 1, 1)

        [Header(Cone)]
        _BaseV("Base V", Range(0, 0.5)) = 0.05
        _ApexV("Apex V", Range(0.5, 1)) = 0.95
        _ConeWidth("Base Half Width", Range(0.05, 0.5)) = 0.42
        _Tilt("View Tilt (ellipse squash)", Range(0, 0.5)) = 0.18

        [Header(Spiral)]
        _Turns("Turns", Range(1, 12)) = 5
        _Speed("Climb Speed", Range(-30, 30)) = 6
        [IntRange] _Strands("Strands", Range(1, 3)) = 1

        [Header(Strand Look)]
        _FrontThickness("Front Thickness", Range(0.001, 0.15)) = 0.045
        _BackThickness("Back Thickness", Range(0.001, 0.15)) = 0.012
        _BackDim("Back Dim", Range(0, 1)) = 0.35
        _Glow("Glow Strength", Range(0, 4)) = 1.2
        _GlowWidth("Glow Width", Range(0.005, 0.3)) = 0.08

        [Header(Masking)]
        _SpriteMask("Sprite Alpha Mask", Range(0, 1)) = 0
        _ApexFade("Apex Fade", Range(0, 0.5)) = 0.1
        _CircleMask("Circle Mask", Range(0, 1)) = 0
        _CircleRadius("Circle Radius", Range(0, 0.75)) = 0.5
        _CircleFeather("Circle Feather", Range(0.001, 0.5)) = 0.1
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

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define PIERCE_TAU 6.28318530718

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
            float4 _MainTex_ST;
            fixed4 _Color;
            float _BaseV;
            float _ApexV;
            float _ConeWidth;
            float _Tilt;
            float _Turns;
            float _Speed;
            float _Strands;
            float _FrontThickness;
            float _BackThickness;
            float _BackDim;
            float _Glow;
            float _GlowWidth;
            float _SpriteMask;
            float _ApexFade;
            float _CircleMask;
            float _CircleRadius;
            float _CircleFeather;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            inline float HelixPhase(float height, float strandPhase)
            {
                return PIERCE_TAU * _Turns * height - _Time.y * _Speed + strandPhase;
            }

            // One strand's contribution at this pixel: core (premultiplied-alpha body) and glow.
            void EvaluateStrand(float2 coneUv, float span, float strandPhase, inout float core, inout float glow)
            {
                // Which helix height projects onto this pixel row? The tilt term shifts each wind's
                // near side down and far side up (the ellipse look); two fixed-point steps land
                // within a fraction of a strand width for any sane tilt.
                float height = coneUv.y;
                for (int step = 0; step < 2; step++)
                {
                    float phase = HelixPhase(height, strandPhase);
                    float radius = _ConeWidth * (1.0 - height);
                    height = saturate(coneUv.y - (_Tilt * radius * sin(phase)) / span);
                }

                float theta = HelixPhase(height, strandPhase);
                float radius = _ConeWidth * (1.0 - height);
                float offset = abs(coneUv.x - radius * cos(theta));

                // sin > 0 is the near half of the wind: thick and bright; the far half recedes.
                float front = 0.5 + 0.5 * sin(theta);
                float thickness = lerp(_BackThickness, _FrontThickness, front);
                float brightness = lerp(_BackDim, 1.0, front);

                core = max(core, smoothstep(thickness, thickness * 0.35, offset) * brightness);
                glow += exp(-offset / max(_GlowWidth, 1e-4)) * brightness;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float span = max(_ApexV - _BaseV, 1e-3);
                float2 coneUv;
                coneUv.x = IN.texcoord.x - 0.5;
                coneUv.y = saturate((IN.texcoord.y - _BaseV) / span);

                float core = 0.0;
                float glow = 0.0;
                int strands = (int)max(_Strands, 1.0);

                [unroll(3)]
                for (int i = 0; i < 3; i++)
                {
                    if (i < strands)
                    {
                        EvaluateStrand(coneUv, span, PIERCE_TAU * i / max(_Strands, 1.0), core, glow);
                    }
                }

                // The wind-up above the apex has no radius left to draw — fade the strand out into
                // the tip instead of collapsing to a flickering point.
                float apexFade = smoothstep(1.0, 1.0 - max(_ApexFade, 1e-3), coneUv.y);
                core *= apexFade;
                glow *= apexFade;

                fixed spriteAlpha = tex2D(_MainTex, IN.texcoord).a;
                float mask = lerp(1.0, spriteAlpha, _SpriteMask);

                // Radial aperture centered on the quad: fade the strands out past _CircleRadius so the
                // effect reads as a disc rather than filling the whole sprite.
                float radial = length(IN.texcoord - 0.5);
                float circle = smoothstep(_CircleRadius, _CircleRadius - max(_CircleFeather, 1e-4), radial);
                mask *= lerp(1.0, circle, _CircleMask);

                fixed4 c;
                c.rgb = IN.color.rgb * (core + glow * _Glow * 0.25) * IN.color.a * mask;
                // Only the strand core eats the background; the glow stays purely additive.
                c.a = saturate(core * IN.color.a) * mask;
                return c;
            }
            ENDCG
        }
    }
}
