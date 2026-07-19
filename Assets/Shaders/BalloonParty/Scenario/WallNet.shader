Shader "BalloonParty/Scenario/WallNet"
{
    // The play-area frame drawn as four procedural net strips (WallNetView builds the meshes). The camera
    // looks top-down, so a net standing along each wall reads edge-on: at REST every across-row is pulled
    // back onto the wall's edge line, collapsing the band to a thin taut line. The shared disturbance
    // field is what reveals it — where anything stamps the field near an edge (a projectile bounce, a pop),
    // the band UNFURLS inward to its full depth and BULGES along the impact direction, exposing the tennis
    // -net weave and its depth right at the contact point. Pure vertex work, no CPU sim (BushLeaf tap).
    // The dreamy glow leans on HDR + bloom, not saturation.
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
        _OpenGain ("Open Gain", Float) = 4.0
        _RestOpen ("Rest Open (thin-line sliver)", Range(0.0, 0.3)) = 0.04
        _BillowAmplitude ("Billow Amplitude (wu)", Float) = 0.35
        // Driven from WallNetView so the shader's rest un-extrude matches the built geometry width.
        _StripWidth ("Strip Width (driven by C#)", Float) = 0.5

        [Header(Idle breathing)]
        _BreatheAmplitude ("Breathe Amplitude (wu)", Float) = 0.03
        _BreatheFrequency ("Breathe Frequency", Float) = 0.7
        _BreatheWaves ("Breathe Waves Along Edge", Float) = 3.0
        // The segment-fade noise (below) also drives the breathing so it drifts organically, not as a
        // rigid marching sine. These dial how much.
        _BreatheNoiseAmount ("Breathe Noise Amount", Range(0, 1)) = 0.6
        _BreatheNoisePhaseWarp ("Breathe Noise Phase Warp", Float) = 2.0

        [Header(Segment fade (scrolling noise))]
        [NoScaleOffset] _NoiseTex ("Tileable Noise (R)", 2D) = "white" {}
        _NoiseScale ("Noise Scale (world)", Float) = 0.6
        _NoiseScroll ("Noise Scroll (xy)", Vector) = (0.05, 0.12, 0, 0)
        _FadeStrength ("Fade Strength", Range(0, 1)) = 0.6
        _FadeContrast ("Fade Contrast", Range(1, 8)) = 3.0
        _FadeFloor ("Fade Floor (min visibility)", Range(0, 1)) = 0.15
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
            };

            // Republished as globals by the disturbance field each blit — no per-material wiring needed.
            sampler2D _DisturbanceTex;
            float2 _FieldBoundsMin;
            float2 _FieldBoundsSize;

            fixed4 _Color;
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
            sampler2D _NoiseTex;
            float _NoiseScale;
            float4 _NoiseScroll;
            float _FadeStrength;
            float _FadeContrast;
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
                // stays at rest.
                float open = max(_RestOpen, saturate(mag * _OpenGain));

                // Un-extrude back to the wall edge line by how far this row sits outward, relaxing as it opens.
                float3 pos = worldRest - outward * (across * _StripWidth) * (1.0 - open);

                // Billow: the field's displacement is the wake (opposite the motion), so negate it to bulge
                // the sheet WITH the motion — a shot heading outward pushes the net further outward, curving
                // it so the top-down view reads its depth.
                float push = -dot(disp, outward.xy);
                pos += outward * push * _BillowAmplitude;

                // Idle breathing: a travelling half-rectified sine along the edge, outward-only so the
                // resting line gently undulates away from the wall and never dips into the play area.
                // The SAME scrolling noise field (shared with the segment fade) is sampled on the net here
                // to make it organic — it warps the wave's phase and swells some segments more than others,
                // all drifting over time. (Default white noise -> a clean sine, so this is a no-op unbound.)
                float2 breatheUV = worldRest.xy * _NoiseScale + _NoiseScroll.xy * _Time.y;
                float breatheNoise = tex2Dlod(_NoiseTex, float4(breatheUV, 0, 0)).r;
                float phase = v.uv0.y * _BreatheWaves * UNITY_TWO_PI
                            + (breatheNoise - 0.5) * _BreatheNoisePhaseWarp;
                float wave = 0.5 + 0.5 * sin(_Time.y * _BreatheFrequency + phase);
                float breathe = wave * lerp(1.0, breatheNoise, _BreatheNoiseAmount) * _BreatheAmplitude;
                pos += outward * breathe;

                o.pos = mul(UNITY_MATRIX_VP, float4(pos, 1.0));
                o.across = v.uv0;
                o.net = v.uv1;
                o.open = open;
                // Anchor the segment-fade noise to the REST world position so the fade pattern sits in
                // world space and drifts by its own scroll, rather than swimming with the billow.
                o.worldXY = worldRest.xy;
                return o;
            }

            // Alpha eaten by a scrolling noise mask so the net fades in and out in drifting segments — two
            // offset samples of the same tileable noise drift past each other for a wispy, non-repeating
            // look (the Sprite/SightSmoke technique), keyed to world space for spatial stability.
            float SegmentFade(float2 worldXY)
            {
                float2 nUV = worldXY * _NoiseScale;
                float2 scroll = _NoiseScroll.xy * _Time.y;
                float a = tex2D(_NoiseTex, nUV + scroll).r;
                float b = tex2D(_NoiseTex, nUV * 1.7 - scroll * 0.6).r;
                float n = a * b;

                float mask = saturate((n - 0.5) * _FadeContrast + 0.5);
                mask = lerp(1.0, mask, _FadeStrength);
                return max(mask, _FadeFloor);
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
                col.a *= net * feather * SegmentFade(i.worldXY);
                return col;
            }
            ENDCG
        }
    }
}
