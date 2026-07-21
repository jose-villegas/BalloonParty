// Procedural reentry-style shield: concentric field-line shells wrapping a comet shape.
// Each layer = an onion shell of a single filled CometSDF (dome circle ∪ parabolic tail).
// The field lines ARE the shield — no solid dome fill; optional dome overlay is separate.
// Dissolve sweeps from dome apex downward. Reveal wipe sweeps apex→tail for layer gain.
// UV warp: velocity ripple + directional lean on bounce. Per-layer flow + color shift.
// Driven by MaterialPropertyBlock: _DissolveProgress[5], _RevealProgress[5], _Color, etc.
Shader "BalloonParty/Display/EMShieldField"
{
    Properties
    {
        [Toggle(EDITOR_PREVIEW)] _EditorPreview("Editor Preview", Float) = 0
        _PreviewLayers("Preview Layers", Float) = 5
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
        _ActiveLayers("Active Layers", Range(0, 30)) = 3

        [Header(Line Appearance)]
        _FieldLineThickness("Line Thickness", Range(0.001, 0.02)) = 0.006
        _GlowWidth("Glow Width", Range(0.005, 0.1)) = 0.03
        _GlowIntensity("Glow Intensity", Range(0, 3)) = 1.2
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 3.0

        [Header(Flow)]
        _FlowSpeed("Flow Speed", Range(0, 8)) = 2.0
        _FlowFrequency("Flow Frequency", Range(1, 20)) = 8.0
        _FlowStrength("Flow Strength", Range(0, 1)) = 0.4

        [Header(Layer Color)]
        _ColorShift("Color Shift (inner to outer)", Range(0, 1)) = 0.0
        _ColorPhase("Color Phase", Vector) = (0.55, 0.70, 0.90, 0)

        [Header(Dome Overlay)]
        _DomeOverlayAlpha("Dome Alpha", Range(0, 1)) = 0.0
        _DomeOverlayWidth("Dome Width", Range(0.02, 0.4)) = 0.15
        _DomeOverlayHeight("Dome Height", Range(0.02, 0.4)) = 0.2
        _DomeOverlayRoundness("Dome Roundness", Range(0.01, 0.5)) = 0.12
        _DomeOverlayFade("Dome Gradient Fade", Range(0, 1)) = 0.7

        [Header(Dissolve)]
        _NoiseScale("Noise Scale", Range(1, 40)) = 8.0
        _NoiseScrollSpeed("Noise Scroll Speed", Float) = 5.0
        [Toggle] _NoiseScrollEnabled("Noise Scroll Enabled", Float) = 1
        _NoiseVelocityIntensity("Noise Velocity Intensity", Range(0, 1)) = 0.0
        _NoiseStartLayer("Noise Start Layer", Float) = 0
        _DirectionalBias("Direction Bias", Range(0, 1)) = 0.6

        [Header(Reveal)]
        _RevealEdge("Reveal Soft Edge", Range(0.005, 0.1)) = 0.04

        [Header(Deformation)]
        _VelocityFactor("Velocity Factor", Range(0, 1)) = 0.0
        _RippleAmplitude("Ripple Amplitude", Range(0, 0.05)) = 0.018
        _RippleFrequency("Ripple Frequency", Range(1, 16)) = 5.0
        _RippleSpeed("Ripple Travel Speed", Range(0, 8)) = 2.0
        _LeanStrength("Lean Strength", Range(0, 2.5)) = 0.6
        _LeanStrengthY("Lean Strength Y", Range(0, 2.5)) = 0.3
        _LeanBendPower("Lean Bend Curve", Range(1, 4)) = 2.0

        [Header(Squash)]
        _SquashMag("Squash Magnitude", Float) = 0.0
        _SquashStrength("Squash Strength", Range(0, 5)) = 0.25
        _SquashNormal("Squash Normal", Vector) = (0,1,0,0)
        _SquashDomeShift("Squash Dome Shift", Range(-0.3, 0.3)) = -0.05

        [Header(Tip)]
        _TipFade("Tip Fade Radius", Range(0.01, 0.15)) = 0.05

        [Header(Shape Mask)]
        _MaskCenterV("Mask Center V", Range(0.1, 0.9)) = 0.5
        _MaskWidth("Mask Half Width", Range(0.05, 0.6)) = 0.4
        _MaskHeight("Mask Half Height", Range(0.1, 0.6)) = 0.5
        _MaskRoundness("Mask Roundness", Range(0.01, 0.5)) = 0.2
        _MaskFade("Mask Fade Softness", Range(0.01, 0.25)) = 0.08
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
            #pragma shader_feature_local EDITOR_PREVIEW
            #include "UnityCG.cginc"

            #define EM_MAX_LAYERS 30

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
            float _PreviewLayers;

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
            float _FlowSpeed;
            float _FlowFrequency;
            float _FlowStrength;
            float _ColorShift;
            float4 _ColorPhase;
            float _NoiseScale;
            float _NoiseScrollSpeed;
            float _NoiseScrollEnabled;
            float _NoiseVelocityIntensity;
            float _NoiseStartLayer;
            float2 _NoiseScrollDir;
            float _SquashMag;
            float _SquashStrength;
            float2 _SquashNormal;
            float _SquashDomeShift;
            float _DirectionalBias;
            float _RevealEdge;
            float _VelocityFactor;
            float _RippleAmplitude;
            float _RippleFrequency;
            float _RippleSpeed;
            float _LeanStrength;
            float _LeanStrengthY;
            float _LeanBendPower;
            float4 _DeformDir;
            float _TipFade;
            float _MaskCenterV;
            float _MaskWidth;
            float _MaskHeight;
            float _MaskRoundness;
            float _MaskFade;

            float _DomeOverlayAlpha;
            float _DomeOverlayWidth;
            float _DomeOverlayHeight;
            float _DomeOverlayRoundness;
            float _DomeOverlayFade;

            float _DissolveProgress[EM_MAX_LAYERS];
            float _RevealProgress[EM_MAX_LAYERS];

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

                #ifdef EDITOR_PREVIEW
                // Integer part = fully visible layers; fractional part = reveal of the next layer
                layers = (int)ceil(clamp(_PreviewLayers, 0, EM_MAX_LAYERS));
                float _previewFrac = frac(_PreviewLayers);
                // When _PreviewLayers is exactly integer, frac=0 means that layer is fully revealed
                float _previewTopReveal = (_previewFrac > 0.001) ? _previewFrac : 1.0;
                int _previewTopIndex = max(layers - 1, 0);
                #endif

                // Geometry anchors (shared by warp, dissolve, tip fade, reveal)
                float apexV = _DomeCenter + _DomeRadius;
                float tipY  = _DomeCenter - (_DomeRadius + _TailLength);
                float totalRange = max(apexV - tipY, 1e-4);

                // ── UV warp: velocity ripple + directional bend ──
                // Bend weight: 0 at dome apex, pow curve to 1 at tip — dome stays, tail bends
                float bendT      = saturate((apexV - uv.y) / totalRange);
                float bendWeight = pow(bendT, _LeanBendPower);

                // Velocity-driven organic ripple (two inharmonic sine waves × noise)
                float warpNoise = ValueNoise(uv * float2(2.5, 1.8)
                    + float2(_Time.y * 0.15, _Time.y * 0.07));
                float wave1  = sin(uv.y * _RippleFrequency       - _Time.y * _RippleSpeed);
                float wave2  = sin(uv.y * _RippleFrequency * 1.6 - _Time.y * _RippleSpeed * 1.3 + 2.1);
                float ripple = (wave1 * 0.6 + wave2 * 0.4) * (0.7 + 0.3 * warpNoise);
                float rippleX = ripple * bendWeight * _RippleAmplitude * _VelocityFactor;

                // Directional bend: single decay vector from bounce impulse
                float2 deformDir = _DeformDir.xy;
                float bendX = deformDir.x * bendWeight * _LeanStrength;
                float bendY = deformDir.y * bendWeight * _LeanStrengthY;

                float2 warpedUV = uv + float2(rippleX + bendX, bendY);

                // Squash: compress along impact normal, area-preserving stretch perpendicular
                float2 squashN = normalize(_SquashNormal.xy + float2(0, 1e-5));
                float2 squashP = float2(-squashN.y, squashN.x);
                float2 fromPivot = warpedUV - float2(0.5, _DomeCenter);
                float squashS = _SquashMag * _SquashStrength;
                float2 squashedUV = float2(0.5, _DomeCenter)
                    + squashN * dot(fromPivot, squashN) * (1.0 + squashS)
                    + squashP * dot(fromPivot, squashP) * (1.0 - squashS * 0.5);

                // Dome tip shifts vertically with squash intensity
                squashedUV.y += squashS * _SquashDomeShift;

                // One SDF evaluation for ALL layers (onion operator) — on warped UV
                float filled = CometSDF(squashedUV);

                // Dissolve noise (on original uv so dissolve anchor stays world-stable)
                // Noise scrolls along the deform curve direction, scaled by projectile speed
                float noiseSpeed = _NoiseScrollSpeed * _VelocityFactor * _NoiseScrollEnabled;
                float2 scrollDir = float2(-_NoiseScrollDir.x * 0.5, 1.0);
                float2 noiseOffset = scrollDir * _Time.y * noiseSpeed;
                float noiseRaw = ValueNoise(uv * _NoiseScale + noiseOffset);
                float noiseVal = lerp(noiseRaw, noiseRaw * _VelocityFactor, _NoiseVelocityIntensity);

                // Dissolve bias anchored to actual dome apex
                float belowApex = saturate((apexV - uv.y) / max(apexV, 1e-4));
                float noiseContrib = noiseVal * (1.0 - _DirectionalBias);
                float dissolveBase = noiseContrib + belowApex * _DirectionalBias;

                // Tip convergence fade (on original uv)
                float tipDist = length(uv - float2(0.5, tipY));
                float tipFade = smoothstep(_TipFade * 0.3, _TipFade, tipDist);

                // Flow tangent (from warped SDF — flow follows the deformed contour)
                float2 grad = float2(ddx(filled), ddy(filled));
                float2 tangent = float2(-grad.y, grad.x);
                float flowCoord = dot(uv - float2(0.5, _DomeCenter), tangent);

                float totalCore = 0.0;
                float totalGlow = 0.0;
                float3 coloredGlow = float3(0, 0, 0);

                [unroll]
                for (int i = 0; i < EM_MAX_LAYERS; i++)
                {
                    if (i >= layers)
                    {
                        continue;
                    }

                    // Dissolve check
                    float speckMask = step(_NoiseStartLayer, float(i));
                    float dissolve = step(dissolveBase, _DissolveProgress[i]);
                    #ifdef EDITOR_PREVIEW
                    dissolve = 0.0;
                    #endif
                    if (dissolve > 0.5)
                    {
                        continue;
                    }

                    // Reveal wipe: sweeps from dome apex to tail tip with soft edge
                    float revealThresh = lerp(apexV + _RevealEdge,
                                              tipY  - _RevealEdge,
                                              _RevealProgress[i]);
                    float revealMask = smoothstep(revealThresh - _RevealEdge,
                                                  revealThresh + _RevealEdge, uv.y);
                    #ifdef EDITOR_PREVIEW
                    // Fully visible layers get revealMask=1; topmost layer uses fractional wipe
                    float previewReveal = (i == _previewTopIndex) ? _previewTopReveal : 1.0;
                    float previewThresh = lerp(apexV + _RevealEdge,
                                               tipY  - _RevealEdge,
                                               previewReveal);
                    revealMask = smoothstep(previewThresh - _RevealEdge,
                                            previewThresh + _RevealEdge, uv.y);
                    #endif

                    // Onion shell: distance to this layer's contour
                    float offset = _BaseRadius + i * _LayerSpacing;
                    float dist = abs(filled - offset);

                    // Pulse
                    float pulse = 0.85 + 0.15 * sin(_Time.y * _PulseSpeed + i * 1.2);

                    // Flow modulation
                    float flow = sin(flowCoord * _FlowFrequency - _Time.y * _FlowSpeed + i * 2.1);
                    float flowMask = 1.0 - _FlowStrength * 0.5 + _FlowStrength * 0.5 * flow;

                    // Core strand
                    float thickness = _FieldLineThickness * pulse;
                    float core = smoothstep(thickness, thickness * 0.15, dist)
                                 * tipFade * flowMask * revealMask;

                    // Glow
                    float glow = exp(-dist / max(_GlowWidth, 1e-4))
                                 * pulse * tipFade * flowMask * revealMask;

                    // Dissolve edge glow
                    float dissolveDist = saturate(1.0 - abs(_DissolveProgress[i] - dissolveBase) * 4.0);
                    glow += dissolveDist * 0.4 * revealMask * speckMask;

                    // Per-layer color shift (cosine palette)
                    float layerT = float(i) / max(float(EM_MAX_LAYERS - 1), 1.0);
                    float3 layerColor = float3(1, 1, 1);
                    if (_ColorShift > 0.001)
                    {
                        float3 phase = _ColorPhase.xyz;
                        layerColor = lerp(float3(1, 1, 1),
                            0.5 + 0.5 * cos(6.283185 * (layerT + phase)),
                            _ColorShift);
                    }

                    totalCore = max(totalCore, core);
                    totalGlow = min(totalGlow + glow, 3.0);
                    coloredGlow += glow * layerColor;
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

                // Shape mask: rounded-rect (capsule when roundness >= min(width, height))
                float2 maskP = uv - float2(0.5, _MaskCenterV);
                float2 maskHalf = float2(_MaskWidth, _MaskHeight);
                float r = min(_MaskRoundness, min(maskHalf.x, maskHalf.y));
                float2 mq = abs(maskP) - maskHalf + r;
                float maskDist = length(max(mq, 0.0)) + min(max(mq.x, mq.y), 0.0) - r;
                float shapeMask = smoothstep(_MaskFade, -_MaskFade, maskDist);

                // Final compositing — glow purely additive RGB, only core affects alpha
                float3 glowColor = (_ColorShift > 0.001)
                    ? coloredGlow / max(totalGlow, 1e-4)
                    : float3(1, 1, 1);
                float glowVal = totalGlow * _GlowIntensity * 0.25 * shapeMask;
                float coreVal = (totalCore + domeOverlay) * shapeMask;

                if (coreVal + glowVal < 0.001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                fixed4 c;
                c.rgb = IN.color.rgb * (coreVal + glowVal * glowColor) * IN.color.a;
                c.a = saturate(coreVal * IN.color.a);
                return c;
            }
            ENDCG
        }
    }
}
