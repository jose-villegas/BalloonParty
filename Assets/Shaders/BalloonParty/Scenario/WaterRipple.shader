Shader "Hidden/BalloonParty/Scenario/WaterRipple"
{
    // The water's surface simulation (WaterRippleBuffer): a shallow-water solver over the disturbance field's bounds,
    // one texel per cell. Each cell holds its height above rest and the flow across its right and top edges. Flow
    // runs from high water to low, which lowers the one and raises the other, so a dip travels outward as a wave and
    // a wave reflects where the screen ends: the edges are walls, and the water rings and sloshes between them.
    // Surface tension pulls on curvature, so short ripples run ahead of long ones and a splash spreads into a train
    // of wavelets instead of one ring. Viscosity smooths the flow at the grid's own scale, where waves would go
    // jagged. A crest steeper than the break slope loses some of its flow. Foam forms only on sharp peaks, where the
    // surface's normal turns suddenly (a crest whose curvature passes the foam curvature), rides the flow a little
    // and fades fast, so it stays on the peaks that make it.
    //
    // Pass 0 (Splat) draws each impact WaterRippleBuffer is pressing into the force target, additively: a soft
    // pressure that pushes the water away and a drag along the impact's heading. Pass 1 (Step) advances the water
    // one fixed step.
    //
    // Each cell computes the flow across all four of its edges, and both cells on an edge compute it from the same
    // texels in the same order, so the water one cell loses the next one gains exactly: no water appears or leaves.
    //
    // A wave equation over the height alone was tried before: with one speed for every wavelength and no flow, its
    // rings interfered into a chop, and drawn rings travelled but never met anything.
    //
    // State: r = height above rest, g = flow across the right edge, b = flow across the top edge, a = foam
    // (ARGBHalf, signed). Units: texels, with the rest depth as 1.
    Properties
    {
        _MainTex      ("Previous State",               2D)    = "black" {}
        [Header(Waves)]
        // How fast a long wave travels. Kept under half a texel a step, past which the solver blows up.
        _WaveSpeed    ("Wave Speed (world/s)",          Float) = 1.5
        // How hard the surface pulls flat on its curvature: short ripples travel faster than long ones by it.
        // Kept under the solver's stable limit.
        _Tension      ("Surface Tension (world²)",      Float) = 0.003
        // How fast the flow loses its strength (the time constant): low fades waves before they cross the screen,
        // high lets them ring between the walls.
        _Fade         ("Fade (seconds)",                Float) = 2.5
        // Smooths the flow over a neighbourhood, more at shorter wavelengths, so the grid's own jaggies die first.
        _Viscosity    ("Viscosity (world²/s)",          Float) = 0.004
        [Header(Impacts)]
        // How deep an impact of full strength presses, in rest depths.
        _Push         ("Push",                          Float) = 0.9
        // How hard an impact drags the water along its heading (a ball's wake).
        _Drag         ("Drag (world/s²)",               Float) = 6
        // An impact's press radius per world unit of its disturbance radius (read by WaterRippleBuffer).
        _PressRadius  ("Press Radius (× impact)",       Float) = 0.5
        [Header(Breakup)]
        // Height change across a texel past which a crest breaks and loses flow.
        _BreakSlope   ("Break Slope (per texel)",       Float) = 0.06
        // How much of a breaking crest's flow it loses each second per unit past the break slope.
        _BreakLoss    ("Break Loss",                    Float) = 400
        // How sharp a peak must be to foam: its curvature (the middle above its four neighbours' mean, times four,
        // in rest depths).
        _FoamCurvature("Foam Curvature",                Float) = 0.12
        // Foam a peak makes each second per unit of curvature past the foam curvature.
        _FoamGain     ("Foam Gain",                     Float) = 40
        // How fast foam fades (the time constant): short, so it stays on the peak that made it.
        _FoamFade     ("Foam Fade (seconds)",           Float) = 0.35
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // Splat: each press as a soft Gaussian. Out: r = pressure, gb = drag.
        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                // xy = position in press radii.
                float2 local  : TEXCOORD0;
                // x = strength, yz = heading.
                float3 press  : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 local : TEXCOORD0;
                float3 press : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.local = v.local;
                o.press = v.press;
                return o;
            }

            float4 frag(v2f IN) : SV_Target
            {
                float weight = exp(-dot(IN.local, IN.local)) * IN.press.x;
                return float4(weight, weight * IN.press.yz, 0.0);
            }
            ENDCG
        }

        // Step.
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            sampler2D _ForceTex;

            float _WaveSpeed;
            float _Tension;
            float _Fade;
            float _Viscosity;
            float _Push;
            float _Drag;
            float _BreakSlope;
            float _BreakLoss;
            float _FoamCurvature;
            float _FoamGain;
            float _FoamFade;

            // Set by WaterRippleBuffer.
            float _StepSeconds;
            float _TexelsPerUnit;

            float4 StateAt(float2 uv, float2 offset)
            {
                return tex2Dlod(_MainTex, float4(uv + offset * _MainTex_TexelSize.xy, 0.0, 0.0));
            }

            float4 ForceAt(float2 uv, float2 offset)
            {
                return tex2Dlod(_ForceTex, float4(uv + offset * _MainTex_TexelSize.xy, 0.0, 0.0));
            }

            // The flow across the edge from cell A to cell B, one step on. Everything it reads, both cells read
            // alike: their height, curvature and pressure, the drag along the edge's axis, the old flow and its
            // curvature along the edge's neighbours.
            float EdgeFlow(float flow, float heightA, float heightB, float curveA, float curveB, float pressA,
                float pressB, float drag, float flowCurve, float open, float3 rates)
            {
                float headA = heightA + pressA * _Push - rates.y * curveA;
                float headB = heightB + pressB * _Push - rates.y * curveB;
                flow += _StepSeconds * (rates.x * (headA - headB) + rates.z * flowCurve + drag);
                float breaking = max(0.0, abs(heightA - heightB) - _BreakSlope);
                flow *= exp(-_StepSeconds / max(_Fade, 0.05)) * saturate(1.0 - _StepSeconds * _BreakLoss * breaking);
                return flow * open;
            }

            float4 frag(v2f_img IN) : SV_Target
            {
                float2 uv = IN.uv;
                float4 c = StateAt(uv, float2(0, 0));
                float4 e = StateAt(uv, float2(1, 0));
                float4 w = StateAt(uv, float2(-1, 0));
                float4 n = StateAt(uv, float2(0, 1));
                float4 s = StateAt(uv, float2(0, -1));
                float4 ee = StateAt(uv, float2(2, 0));
                float4 ww = StateAt(uv, float2(-2, 0));
                float4 nn = StateAt(uv, float2(0, 2));
                float4 ss = StateAt(uv, float2(0, -2));
                float4 ne = StateAt(uv, float2(1, 1));
                float4 nw = StateAt(uv, float2(-1, 1));
                float4 se = StateAt(uv, float2(1, -1));
                float4 sw = StateAt(uv, float2(-1, -1));

                float4 fc = ForceAt(uv, float2(0, 0));
                float4 fe = ForceAt(uv, float2(1, 0));
                float4 fw = ForceAt(uv, float2(-1, 0));
                float4 fn = ForceAt(uv, float2(0, 1));
                float4 fs = ForceAt(uv, float2(0, -1));

                float curveC = e.r + w.r + n.r + s.r - 4.0 * c.r;
                float curveE = ee.r + c.r + ne.r + se.r - 4.0 * e.r;
                float curveW = c.r + ww.r + nw.r + sw.r - 4.0 * w.r;
                float curveN = ne.r + nw.r + nn.r + c.r - 4.0 * n.r;
                float curveS = se.r + sw.r + c.r + ss.r - 4.0 * s.r;

                // In texels: wave speed², tension and viscosity, each held under the explicit step's limit.
                float speed = min(_WaveSpeed * _TexelsPerUnit, 0.5 / _StepSeconds);
                float speed2 = speed * speed;
                float courant2 = speed2 * _StepSeconds * _StepSeconds;
                float tension = clamp(_Tension * _TexelsPerUnit * _TexelsPerUnit, 0.0, (0.4 / courant2 - 1.0) / 8.0);
                float viscosity = clamp(_Viscosity * _TexelsPerUnit * _TexelsPerUnit, 0.0, 0.1 / _StepSeconds);
                float3 rates = float3(speed2, tension, viscosity);
                float drag = _Drag * _TexelsPerUnit;

                // The screen's edges are walls: nothing flows across them.
                float2 cell = uv * _MainTex_TexelSize.zw;
                float openRight = cell.x < _MainTex_TexelSize.z - 1.0 ? 1.0 : 0.0;
                float openLeft = cell.x > 1.0 ? 1.0 : 0.0;
                float openTop = cell.y < _MainTex_TexelSize.w - 1.0 ? 1.0 : 0.0;
                float openBottom = cell.y > 1.0 ? 1.0 : 0.0;

                float right = EdgeFlow(c.g, c.r, e.r, curveC, curveE, fc.r, fe.r, 0.5 * (fc.g + fe.g) * drag,
                    e.g + w.g + n.g + s.g - 4.0 * c.g, openRight, rates);
                float left = EdgeFlow(w.g, w.r, c.r, curveW, curveC, fw.r, fc.r, 0.5 * (fw.g + fc.g) * drag,
                    c.g + ww.g + nw.g + sw.g - 4.0 * w.g, openLeft, rates);
                float top = EdgeFlow(c.b, c.r, n.r, curveC, curveN, fc.r, fn.r, 0.5 * (fc.b + fn.b) * drag,
                    n.b + s.b + e.b + w.b - 4.0 * c.b, openTop, rates);
                float bottom = EdgeFlow(s.b, s.r, c.r, curveS, curveC, fs.r, fc.r, 0.5 * (fs.b + fc.b) * drag,
                    c.b + ss.b + se.b + sw.b - 4.0 * s.b, openBottom, rates);

                float height = c.r - _StepSeconds * (right - left + top - bottom);

                // Foam rides the flow (traced back along it), fades, and grows on sharp peaks: a crest (above rest) whose
                // curvature, where the normal turns, passes the foam curvature.
                float2 velocity = float2(right + left, top + bottom) * 0.5;
                float foam = tex2Dlod(_MainTex, float4(uv - velocity * _StepSeconds * _MainTex_TexelSize.xy, 0, 0)).a;
                foam *= exp(-_StepSeconds / max(_FoamFade, 0.05));
                float peak = c.r > 0.0 ? max(0.0, -curveC - _FoamCurvature) : 0.0;
                foam += _StepSeconds * _FoamGain * peak;

                return float4(clamp(height, -2.0, 2.0), right, top, saturate(foam));
            }
            ENDCG
        }
    }
}
