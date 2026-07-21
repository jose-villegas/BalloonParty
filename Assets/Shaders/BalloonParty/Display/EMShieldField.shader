// Procedural EM shield: concentric field-line shells wrapping a morphable SDF shape.
// Shape morphs between a pure circle (_ShapeLerp=0) and a comet with tail (_ShapeLerp=1).
// Each layer = an onion shell of MorphedSDF. The field lines ARE the shield.
// Dissolve sweeps from apex downward. Reveal wipe sweeps apex→tail for layer gain.
// UV warp: velocity ripple. Per-layer flow + color shift.
// Driven by MaterialPropertyBlock: _DissolveProgress[], _RevealProgress[], _Color, etc.
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
        _CircleRadius("Circle Inner Radius", Range(0, 0.3)) = 0.05
        _CircleCenter("Circle Center V", Range(0.3, 0.7)) = 0.5
        _TailLength("Tail Length", Range(0.05, 0.6)) = 0.3
        _TailWidth("Tail Base Width", Range(0, 0.4)) = 0.12
        _TailPower("Tail Convergence", Range(0.5, 4.0)) = 2.0
        _JunctionSmooth("Junction Smoothness", Range(0.005, 0.15)) = 0.04
        _CometWidthScale("Comet Width Scale", Range(0.3, 3.0)) = 1.0
        _ShapeLerp("Shape Lerp (0=circle, 1=tail)", Range(0, 1)) = 1.0

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


        [Header(Dissolve)]
        _NoiseScale("Noise Scale", Range(1, 100)) = 8.0
        _NoiseScrollSpeed("Noise Scroll Speed", Float) = 5.0
        [Toggle] _NoiseEnabled("Noise Enabled", Float) = 1
        _NoiseIntensity("Noise Intensity", Range(0, 1)) = 0.0
        _NoiseVelocityIntensity("Noise Velocity Intensity", Range(0, 1)) = 0.0
        _DirectionalBias("Direction Bias", Range(0, 1)) = 0.6

        [Header(Reveal)]
        _RevealEdge("Reveal Soft Edge", Range(0.005, 0.1)) = 0.04

        [Header(Deformation)]
        _VelocityFactor("Velocity Factor", Range(0, 1)) = 0.0
        _RippleAmplitude("Ripple Amplitude", Range(0, 0.05)) = 0.018
        _RippleFrequency("Ripple Frequency", Range(1, 16)) = 5.0
        _RippleSpeed("Ripple Travel Speed", Range(0, 8)) = 2.0

        [Header(Tip)]
        _TipFade("Tip Fade Radius", Range(0.01, 0.15)) = 0.05

        [Header(Shape Mask)]
        _MaskCenterV("Mask Center V", Range(0.1, 0.9)) = 0.5
        _MaskWidth("Mask Half Width", Range(0.05, 0.6)) = 0.4
        _CircleMaskWidth("Circle Mask Half Width", Range(0.05, 0.6)) = 0.5
        _MaskHeight("Mask Half Height", Range(0.1, 0.6)) = 0.5
        _MaskRoundness("Mask Roundness", Range(0.01, 0.5)) = 0.2
        _MaskFade("Mask Fade Softness", Range(0.01, 0.25)) = 0.08

        [Header(Squash)]
        _SquashAmount("Squash Amount", Range(0, 1)) = 0.0
        _SquashAxis("Squash Axis", Vector) = (0, 1, 0, 0)
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
            float _CircleRadius;
            float _CircleCenter;
            float _TailLength;
            float _TailWidth;
            float _TailPower;
            float _JunctionSmooth;
            float _CometWidthScale;
            float _ShapeLerp;
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
            float _NoiseEnabled;
            float _NoiseIntensity;
            float _NoiseVelocityIntensity;
            float2 _NoiseScrollDir;
            float _DirectionalBias;
            float _RevealEdge;
            float _VelocityFactor;
            float _RippleAmplitude;
            float _RippleFrequency;
            float _RippleSpeed;
            float _TipFade;
            float _MaskCenterV;
            float _MaskWidth;
            float _CircleMaskWidth;
            float _MaskHeight;
            float _MaskRoundness;
            float _MaskFade;

            float _SquashAmount;
            float2 _SquashAxis;

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
            // Morphable SDF: interpolates between full circle (_ShapeLerp=0) and
            // comet shape (_ShapeLerp=1) by smoothly retracting tail and expanding dome.
            inline float MorphedSDF(float2 uv)
            {
                // Morph: tail collapses, dome shrinks to circle radius
                float sl = _ShapeLerp;
                float morphedRadius = lerp(_CircleRadius, _DomeRadius, sl);
                float morphedCenter = lerp(_CircleCenter, _DomeCenter, sl);

                float2 p = uv - float2(0.5, morphedCenter);
                // Configurable horizontal scale for the comet shape
                p.x *= lerp(1.0, _CometWidthScale, sl);

                // Squash deformation: compress along impact axis, expand perpendicular (area-preserving)
                // Only active in circle mode (fades with 1-sl)
                float squash = _SquashAmount * (1.0 - sl);
                if (squash > 0.001)
                {
                    float2 axis = normalize(_SquashAxis + float2(1e-6, 0));
                    float2 perp = float2(-axis.y, axis.x);
                    float along = dot(p, axis);
                    float side  = dot(p, perp);
                    float compress = 1.0 - squash * 0.25;
                    float expand   = 1.0 / max(compress, 0.5);
                    p = axis * (along * compress) + perp * (side * expand);
                }

                float dDome = length(p) - morphedRadius;

                // At sl=0 return pure circle; at sl=1 full comet with tail
                float morphedTailLength = _TailLength * sl;
                float morphedTailWidth  = _TailWidth * sl;
                float tailTip = morphedRadius + morphedTailLength;
                float t = saturate(-p.y / max(tailTip, 1e-4));
                float w = morphedTailWidth * pow(max(1.0 - t, 0.0), _TailPower);
                float dTail = abs(p.x) - w;
                dTail = max(dTail, p.y);

                float k = _JunctionSmooth;
                float h = saturate(0.5 + 0.5 * (dTail - dDome) / max(k, 1e-5));
                float comet = lerp(dTail, dDome, h) - k * h * (1.0 - h);

                // Blend: pure circle at sl=0, full comet at sl=1
                return lerp(dDome, comet, sl);
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

                // Geometry anchors (use morphed center/radius for correct dissolve/reveal)
                float sl = _ShapeLerp;
                float morphedCenter = lerp(_CircleCenter, _DomeCenter, sl);
                float morphedRadius = lerp(_CircleRadius, _DomeRadius, sl);
                float morphedTailLength = _TailLength * sl;
                // Use shell extent as minimum so anchors stay valid in circle mode
                float shellExtent = _BaseRadius + max(float(layers) - 1.0, 0.0) * _LayerSpacing;
                float apexV = morphedCenter + max(morphedRadius, shellExtent);
                float tipY  = morphedCenter - max(morphedRadius + morphedTailLength, shellExtent);
                float totalRange = max(apexV - tipY, 1e-4);

                // ── UV warp: velocity ripple ──
                float bendT      = saturate((apexV - uv.y) / totalRange);
                float bendWeight = pow(bendT, 2.0);

                // Velocity-driven organic ripple (two inharmonic sine waves × noise)
                float warpNoise = ValueNoise(uv * float2(2.5, 1.8)
                    + float2(_Time.y * 0.15, _Time.y * 0.07));
                float wave1  = sin(uv.y * _RippleFrequency       - _Time.y * _RippleSpeed);
                float wave2  = sin(uv.y * _RippleFrequency * 1.6 - _Time.y * _RippleSpeed * 1.3 + 2.1);
                float ripple = (wave1 * 0.6 + wave2 * 0.4) * (0.7 + 0.3 * warpNoise);
                float rippleX = ripple * bendWeight * _RippleAmplitude * _VelocityFactor;

                float2 warpedUV = uv + float2(rippleX, 0);

                // One SDF evaluation for ALL layers (onion operator) — on warped UV
                float filled = MorphedSDF(warpedUV);

                // Dissolve noise (on original uv so dissolve anchor stays world-stable)
                float dissolveBase = 0.0;
                float noiseAlpha = _NoiseIntensity;
                if (_NoiseEnabled > 0.5 && noiseAlpha > 0.001)
                {
                    float noiseSpeed = _NoiseScrollSpeed * _VelocityFactor;
                    float2 scrollDir = float2(-_NoiseScrollDir.x * 0.5, 1.0);
                    float2 noiseOffset = scrollDir * _Time.y * noiseSpeed;
                    float noiseRaw = ValueNoise(uv * _NoiseScale + noiseOffset);
                    float noiseVal = lerp(noiseRaw, noiseRaw * _VelocityFactor, _NoiseVelocityIntensity);
                    float belowApex = saturate((apexV - uv.y) / max(apexV, 1e-4));
                    float noiseContrib = noiseVal * (1.0 - _DirectionalBias);
                    dissolveBase = noiseContrib + belowApex * _DirectionalBias;
                }

                // Tip convergence fade (on original uv) — fades out in circle mode
                float tipDist = length(uv - float2(0.5, tipY));
                float tipFade = lerp(1.0, smoothstep(_TipFade * 0.3, _TipFade, tipDist), sl);

                // Flow tangent (from warped SDF — flow follows the deformed contour)
                float2 grad = float2(ddx(filled), ddy(filled));
                float2 tangent = float2(-grad.y, grad.x);
                float flowCoord = dot(uv - float2(0.5, morphedCenter), tangent);

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

                    // Dissolve check — layer is dissolved when its progress exceeds the noise threshold
                    float dissolve = (_DissolveProgress[i] > 0.001) ? step(dissolveBase, _DissolveProgress[i]) : 0.0;
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

                    // Dissolve edge glow (scaled by noise intensity)
                    float dissolveDist = saturate(1.0 - abs(_DissolveProgress[i] - dissolveBase) * 4.0);
                    glow += dissolveDist * 0.4 * revealMask * noiseAlpha;

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

                // Shape mask: rounded-rect (capsule when roundness >= min(width, height))
                float2 maskP = uv - float2(0.5, _MaskCenterV);
                float2 maskHalf = float2(lerp(_CircleMaskWidth, _MaskWidth, sl), _MaskHeight);
                float r = min(_MaskRoundness, min(maskHalf.x, maskHalf.y));
                float2 mq = abs(maskP) - maskHalf + r;
                float maskDist = length(max(mq, 0.0)) + min(max(mq.x, mq.y), 0.0) - r;
                float shapeMask = smoothstep(_MaskFade, -_MaskFade, maskDist);

                // Final compositing — glow purely additive RGB, only core affects alpha
                float3 glowColor = (_ColorShift > 0.001)
                    ? coloredGlow / max(totalGlow, 1e-4)
                    : float3(1, 1, 1);
                float glowVal = totalGlow * _GlowIntensity * 0.25 * shapeMask;
                float coreVal = totalCore * shapeMask;

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
