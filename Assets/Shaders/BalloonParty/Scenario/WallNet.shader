Shader "BalloonParty/Scenario/WallNet"
{
    // The play-area frame drawn as four procedural net strips (WallNetView builds the meshes). The camera
    // looks top-down, so a net standing along each wall reads edge-on: at REST every across-row is pulled
    // back onto the wall's edge line, collapsing the band to a thin taut line. The shared disturbance
    // field is what reveals it — where anything stamps the field near an edge (a projectile bounce, a pop),
    // the band UNFURLS inward to its full depth and BULGES along the impact direction, exposing the tennis
    // -net weave and its depth right at the contact point. Pure vertex work, no CPU sim (BushLeaf tap).
    // The dreamy glow leans on HDR + bloom, not saturation. Its VISIBILITY (and idle breathing) is driven
    // by the shared CloudField (CloudField.cginc), so the net shows where there's cloud and fades where
    // there isn't — it reads as part of the clouds rather than a separate frame.
    Properties
    {
        [HDR] _Color ("Net Color (HDR)", Color) = (1.4, 1.55, 1.7, 0.9)
        _LineWidth ("Line Half-Width (uv)", Range(0.01, 0.5)) = 0.08
        _LineSoftness ("Line Softness (uv)", Range(0.0, 0.5)) = 0.06
        _EdgeFeather ("Band Edge Feather (across)", Range(0.0, 0.5)) = 0.12
        _DepthShade ("Inner-Edge Depth Shade", Range(0.0, 1.0)) = 0.55
        // Receive-only scene light: 0 = always the authored HDR colour, 1 = fully take the light's
        // palette colour at max presence (no RegisterLight / cast).
        _LightInfluence ("Light Influence", Range(0.0, 1.0)) = 1.0
        // How strongly the light field's R (local magnitude) drives the colour replacement — higher
        // reaches full replacement with less light.
        _LightColorRamp ("Light Color Ramp (R gain)", Float) = 1.0

        [Header(Reveal (driven by the disturbance field))]
        // The net colour shifts from _Color (at rest) toward this as it gets more strongly disturbed —
        // a heat gradient at the impact. Gain shapes how quickly it reaches full.
        [HDR] _DisturbedColor ("Disturbed Net Color (HDR)", Color) = (1.8, 1.0, 0.5, 1.0)
        _DisturbColorGain ("Disturb Color Gain", Float) = 1.0
        // Visibility the net fades toward at MAX disturbance (as the net moves from the field). 1 = reveal
        // where struck (even off-cloud); 0 = fade out on impact. Blends from the cloud visibility by how
        // disturbed the spot is.
        _DisturbVisibilityTarget ("Visibility At Max Disturbance", Range(0, 1)) = 1.0
        _OpenGain ("Open Gain", Float) = 4.0
        _RestOpen ("Rest Open (thin-line sliver)", Range(0.0, 0.3)) = 0.04
        _BillowAmplitude ("Billow Amplitude (wu)", Float) = 0.35
        // Driven from WallNetView so the shader's rest un-extrude matches the built geometry width.
        _StripWidth ("Strip Width (driven by C#)", Float) = 0.5

        [Header(Idle breathing)]
        _BreatheAmplitude ("Breathe Amplitude (wu)", Float) = 0.03
        _BreatheFrequency ("Breathe Frequency", Float) = 0.7
        _BreatheWaves ("Breathe Waves Along Edge", Float) = 3.0
        // The shared cloud field also drives the breathing so it drifts organically, not as a rigid
        // marching sine. These dial how much.
        _BreatheNoiseAmount ("Breathe Cloud Amount", Range(0, 1)) = 0.6
        _BreatheNoisePhaseWarp ("Breathe Cloud Phase Warp", Float) = 2.0

        [Header(Cloud visibility (shared CloudField))]
        // Soft threshold on the cloud's smooth intensity: below Low the net is invisible (no-cloud), then
        // a smooth ramp to full by High — a wide band keeps it from segmenting.
        _FadeLow ("Cloud Fade-In Low", Range(0, 1)) = 0.35
        _FadeHigh ("Cloud Fade-In High", Range(0, 1)) = 0.75
        _FadeFloor ("Fade Floor (min visibility)", Range(0, 1)) = 0.0
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
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/CloudField.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 across : TEXCOORD0;
                float2 net : TEXCOORD1;
                float open : TEXCOORD2;
                float2 worldXY : TEXCOORD3;
                float disturb : TEXCOORD4;
            };

            // Republished as globals by the disturbance field each blit — no per-material wiring needed.
            sampler2D _DisturbanceTex;
            float2 _FieldBoundsMin;
            float2 _FieldBoundsSize;

            fixed4 _Color;
            fixed4 _DisturbedColor;
            float _DisturbColorGain;
            float _DisturbVisibilityTarget;
            float _LineWidth;
            float _LineSoftness;
            float _EdgeFeather;
            float _DepthShade;
            float _LightInfluence;
            float _LightColorRamp;
            float _OpenGain;
            float _RestOpen;
            float _BillowAmplitude;
            float _StripWidth;
            float _BreatheAmplitude;
            float _BreatheFrequency;
            float _BreatheWaves;
            float _BreatheNoiseAmount;
            float _BreatheNoisePhaseWarp;
            float _FadeLow;
            float _FadeHigh;
            float _FadeFloor;

            v2f vert(appdata v)
            {
                v2f o;

                // Full-depth rest position (the mesh is built at full width, extruded OUTWARD from the wall)
                // and the edge's outward normal (away from the play area).
                float3 worldRest = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 outward = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float across = v.uv0.x; // 0 at the wall edge, 1 at the outer lip

                // Sample the disturbance at this row's full-depth rest point so depth reveals with variation.
                float2 fieldUV = (worldRest.xy - _FieldBoundsMin) / _FieldBoundsSize;
                float2 disp = (tex2Dlod(_DisturbanceTex, float4(fieldUV, 0, 0)).gb - 0.5) * 2.0;
                float mag = length(disp);

                // Unfurl: 0 -> flush to the wall edge line (thin line), 1 -> full depth outward. A sliver
                // stays at rest. disturbAmt (without the rest floor) also drives the colour gradient.
                float disturbAmt = saturate(mag * _OpenGain);
                float open = max(_RestOpen, disturbAmt);

                // Un-extrude back to the wall edge line by how far this row sits outward, relaxing as it opens.
                float3 pos = worldRest - outward * (across * _StripWidth) * (1.0 - open);

                // Billow: the field's displacement is the wake (opposite the motion), so negate it to bulge
                // the sheet WITH the motion — a shot heading outward pushes the net further outward, curving
                // it so the top-down view reads its depth.
                float push = -dot(disp, outward.xy);
                pos += outward * push * _BillowAmplitude;

                // Idle breathing: a travelling half-rectified sine along the edge, outward-only so the
                // resting line gently undulates away from the wall and never dips into the play area. The
                // SHARED cloud field is sampled on the net here to make it organic — it warps the wave's
                // phase and swells some segments more than others, all drifting with the clouds.
                float breatheNoise = CloudFieldNoiseLOD(worldRest.xy);
                float phase = v.uv0.y * _BreatheWaves * UNITY_TWO_PI
                            + (breatheNoise - 0.5) * _BreatheNoisePhaseWarp;
                float wave = 0.5 + 0.5 * sin(_Time.y * _BreatheFrequency + phase);
                float breathe = wave * lerp(1.0, breatheNoise, _BreatheNoiseAmount) * _BreatheAmplitude;
                pos += outward * breathe;

                o.pos = mul(UNITY_MATRIX_VP, float4(pos, 1.0));
                o.across = v.uv0;
                o.net = v.uv1;
                o.open = open;
                // Anchor the cloud/light sampling to the REST world position so it sits in world space
                // rather than swimming with the billow.
                o.worldXY = worldRest.xy;
                o.disturb = disturbAmt;
                return o;
            }

            // Visibility from the SHARED cloud field's SMOOTH intensity (G, not the near-binary density):
            // a soft threshold so the net is invisible below the cloud onset (no-cloud ground) and ramps up
            // smoothly — blending with the gradient without segmenting. Gated to full intensity (1) when the
            // cloud field isn't in the scene, so the net stays fully visible without it.
            float CloudVisibility(float2 worldXY)
            {
                float intensity = lerp(1.0, CloudFieldNoise(worldXY), _CloudFieldActive);
                float vis = smoothstep(_FadeLow, _FadeHigh, intensity);
                return max(vis, _FadeFloor);
            }

            // 1 on the thin band around each integer boundary of `coord`, 0 in the cell interior.
            float GridLine(float coord)
            {
                float dist = abs(frac(coord) - 0.5);
                dist = 0.5 - dist; // distance to the nearest integer boundary
                float soft = max(_LineSoftness, 1e-4); // a zero-width smoothstep is undefined on some GPUs
                return 1.0 - smoothstep(_LineWidth, _LineWidth + soft, dist);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float net = max(GridLine(i.net.x), GridLine(i.net.y));

                // Soft band edges across the width so the strip fades in rather than cutting a hard line.
                float u = i.across.x;
                float feather_w = max(_EdgeFeather, 1e-4);
                float feather = smoothstep(0.0, feather_w, u) * smoothstep(0.0, feather_w, 1.0 - u);

                fixed4 col = _Color;
                // Heat gradient: shift the net colour toward the disturbed colour by how strongly this
                // spot is disturbed (before the scene-light tint, so a nearby light still overrides).
                col.rgb = lerp(col.rgb, _DisturbedColor.rgb, saturate(i.disturb * _DisturbColorGain));
                // Receive-only scene light: replace the base colour with the PALETTE-indexed colour of a
                // nearby tagged light, blended by that light's local presence — so away from lights the net
                // keeps its authored HDR colour (no white-out), and only where a coloured light sits does it
                // take that light's palette hue. No brightness multiply, no cast.
                float3 keyColor = _SceneLightColor.a > 0.5 ? _SceneLightColor.rgb : float3(1.0, 1.0, 1.0);
                float3 palette = SceneLightPaletteColorAtLOD(SceneLightFieldUV(i.worldXY), keyColor);
                // R (local light magnitude) drives the replacement intensity, scaled by the tunable ramp.
                float presence = saturate(SceneLightFieldSampleLOD(i.worldXY).r * _LightColorRamp);
                col.rgb = lerp(col.rgb, palette, presence * _LightInfluence);
                // Darken toward the inner lip for a curled-depth read as the band opens.
                col.rgb *= lerp(1.0, _DepthShade, u * i.open);
                // Cloud-driven visibility, blended toward the disturbance target by how disturbed this
                // spot is — so a struck section reveals (or fades out) regardless of the cloud there.
                float vis = lerp(CloudVisibility(i.worldXY), _DisturbVisibilityTarget, i.disturb);
                col.a *= net * feather * vis;
                return col;
            }
            ENDCG
        }
    }
}
