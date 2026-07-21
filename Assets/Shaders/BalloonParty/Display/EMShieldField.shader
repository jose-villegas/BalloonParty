// Procedural reentry-style shield: concentric field-line shells wrapping a comet shape.
// Each layer = an onion shell of a single filled CometSDF (dome circle ∪ parabolic tail).
// The field lines ARE the shield — no solid dome fill; optional dome overlay is separate.
// Dissolve sweeps from dome apex downward. Tip fade prevents convergence bright-spot.
// Driven by MaterialPropertyBlock: _DissolveProgress[5], _Color, _ActiveLayers.
Shader "BalloonParty/Display/EMShieldField"
{
    Properties
    {
        _MainTex("Sprite (alpha mask)", 2D) = "white" {}
        [HDR] _Color("Field Tint", Color) = (0.5, 0.8, 1, 1)

        [Header(Comet Shape)]
        _DomeCenter("Dome Center V", Range(0.3, 0.8)) = 0.6
        _DomeRadius("Dome Radius", Range(0.05, 0.4)) = 0.18
        _TailLength("Tail Length", Range(0.05, 0.6)) = 0.3
        _TailWidth("Tail Base Width", Range(0, 0.4)) = 0.12
        _TailPower("Tail Convergence", Range(0.5, 4.0)) = 2.0
        _JunctionSmooth("Junction Smoothness", Range(0.005, 0.15)) = 0.04

        [Header(Shells)]
        _BaseRadius("Innermost Offset", Range(0.01, 0.2)) = 0.07
        _LayerSpacing("Layer Spacing", Range(0.005, 0.06)) = 0.025
        _ActiveLayers("Active Layers", Range(0, 5)) = 3

        [Header(Line Appearance)]
        _FieldLineThickness("Line Thickness", Range(0.001, 0.02)) = 0.006
        _GlowWidth("Glow Width", Range(0.005, 0.1)) = 0.03
        _GlowIntensity("Glow Intensity", Range(0, 3)) = 1.2
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 3.0

        [Header(Dome Overlay)]
        _DomeOverlayAlpha("Dome Alpha", Range(0, 1)) = 0.0
        _DomeOverlayWidth("Dome Width", Range(0.02, 0.4)) = 0.15
        _DomeOverlayHeight("Dome Height", Range(0.02, 0.4)) = 0.2
        _DomeOverlayRoundness("Dome Roundness", Range(0.01, 0.5)) = 0.12
        _DomeOverlayFade("Dome Gradient Fade", Range(0, 1)) = 0.7

        [Header(Dissolve)]
        _NoiseScale("Noise Scale", Range(1, 20)) = 8.0
        _DirectionalBias("Direction Bias", Range(0, 1)) = 0.6

        [Header(Tip)]
        _TipFade("Tip Fade Radius", Range(0.01, 0.15)) = 0.05

        [Header(Edge)]
        _EdgeFade("Edge Fade Width", Range(0.01, 0.2)) = 0.08
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
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define EM_MAX_LAYERS 5

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

            float _DomeCenter;
            float _DomeRadius;
            float _TailLength;
            float _TailWidth;
            float _TailPower;
            float _JunctionSmooth;
            float _BaseRadius;
            float _LayerSpacing;
            float _ActiveLayers;
            float _FieldLineThickness;
            float _GlowWidth;
            float _GlowIntensity;
            float _PulseSpeed;
            float _NoiseScale;
            float _DirectionalBias;
            float _TipFade;
            float _EdgeFade;

            float _DomeOverlayAlpha;
            float _DomeOverlayWidth;
            float _DomeOverlayHeight;
            float _DomeOverlayRoundness;
            float _DomeOverlayFade;

            float _DissolveProgress[EM_MAX_LAYERS];

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // ── Noise ──────────────────────────────────────────────

            inline float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            inline float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // ── Filled comet SDF ───────────────────────────────────
            // Smooth union of dome circle + parabolic converging tail.
            // Negative inside the shape, positive outside.
            // All layers derive from this single evaluation via the onion operator.
            inline float CometSDF(float2 uv)
            {
                float2 p = uv - float2(0.5, _DomeCenter);

                float dDome = length(p) - _DomeRadius;

                float tailTip = _DomeRadius + _TailLength;
                float t = saturate(-p.y / max(tailTip, 1e-4));
                float w = _TailWidth * pow(max(1.0 - t, 0.0), _TailPower);
                float dTail = abs(p.x) - w;
                dTail = max(dTail, p.y);

                float k = _JunctionSmooth;
                float h = saturate(0.5 + 0.5 * (dTail - dDome) / max(k, 1e-5));
                return lerp(dTail, dDome, h) - k * h * (1.0 - h);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                int layers = (int)clamp(_ActiveLayers, 0, EM_MAX_LAYERS);

                // One SDF evaluation for ALL layers (onion operator)
                float filled = CometSDF(uv);

                // Dissolve noise (computed once, shared across layers)
                float noiseVal = ValueNoise(uv * _NoiseScale + _Time.y * 0.3);

                // Dissolve bias anchored to actual dome apex, not UV top
                float apexV = _DomeCenter + _DomeRadius;
                float belowApex = saturate((apexV - uv.y) / max(apexV, 1e-4));
                float noiseContrib = noiseVal * (1.0 - _DirectionalBias);
                float dissolveBase = noiseContrib + belowApex * _DirectionalBias;

                // Tip convergence fade: prevent bright dot where all lines meet
                float tipY = _DomeCenter - (_DomeRadius + _TailLength);
                float tipDist = length(uv - float2(0.5, tipY));
                float tipFade = smoothstep(_TipFade * 0.3, _TipFade, tipDist);

                float totalCore = 0.0;
                float totalGlow = 0.0;

                [unroll]
                for (int i = 0; i < EM_MAX_LAYERS; i++)
                {
                    if (i >= layers)
                    {
                        continue;
                    }

                    // Dissolve check
                    float dissolve = step(dissolveBase, _DissolveProgress[i]);
                    if (dissolve > 0.5)
                    {
                        continue;
                    }

                    // Onion shell: distance to this layer's contour
                    float offset = _BaseRadius + i * _LayerSpacing;
                    float dist = abs(filled - offset);

                    // Pulse
                    float pulse = 0.85 + 0.15 * sin(_Time.y * _PulseSpeed + i * 1.2);

                    // Core strand
                    float thickness = _FieldLineThickness * pulse;
                    float core = smoothstep(thickness, thickness * 0.15, dist) * tipFade;

                    // Glow
                    float glow = exp(-dist / max(_GlowWidth, 1e-4)) * pulse * tipFade;

                    // Dissolve edge glow
                    float dissolveDist = saturate(1.0 - abs(_DissolveProgress[i] - dissolveBase) * 4.0);
                    glow += dissolveDist * 0.4;

                    totalCore = max(totalCore, core);
                    totalGlow = min(totalGlow + glow, 3.0);
                }

                // Dome overlay: separate filled glow element over the top
                float domeOverlay = 0.0;
                if (_DomeOverlayAlpha > 0.001)
                {
                    float2 domeP = uv - float2(0.5, _DomeCenter + _DomeOverlayHeight * 0.3);
                    float2 domeHalf = float2(_DomeOverlayWidth, _DomeOverlayHeight);
                    float2 q = abs(domeP) - domeHalf + _DomeOverlayRoundness;
                    float domeDist = length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - _DomeOverlayRoundness;
                    float domeFill = smoothstep(0.005, -0.005, domeDist);
                    float gradientT = saturate((domeP.y + _DomeOverlayHeight) / max(_DomeOverlayHeight * 2.0, 1e-4));
                    float gradient = lerp(1.0 - _DomeOverlayFade, 1.0, gradientT);
                    domeOverlay = domeFill * gradient * _DomeOverlayAlpha;
                }

                // Edge fade
                float2 fromCenter = uv - 0.5;
                float edgeDist = max(abs(fromCenter.x), abs(fromCenter.y));
                float edgeFade = smoothstep(0.5, 0.5 - _EdgeFade, edgeDist);

                // Final compositing — glow purely additive RGB, only core affects alpha
                float glowRGB = totalGlow * _GlowIntensity * 0.25 * edgeFade;
                float coreRGB = (totalCore + domeOverlay) * edgeFade;

                if (coreRGB + glowRGB < 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                fixed4 c;
                c.rgb = IN.color.rgb * (coreRGB + glowRGB) * IN.color.a;
                c.a = saturate(coreRGB * IN.color.a);
                return c;
            }
            ENDCG
        }
    }
}
