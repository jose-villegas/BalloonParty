Shader "Hidden/BalloonParty/Scenario/WaterRipple"
{
    // The water's surface simulation (WaterRippleBuffer): a shallow-water solver over the disturbance field's bounds,
    // one texel per cell. Each cell holds its height above rest and the flow across its right and top edges. Flow
    // runs from high water to low, which lowers the one and raises the other, so a dip travels outward as a wave and
    // a wave reflects where the screen ends: the edges are walls, and the water rings and sloshes between them.
    // Surface tension pulls on curvature, so short ripples run ahead of long ones and a splash spreads into a train
    // of wavelets instead of one ring. Viscosity smooths the flow at the grid's own scale, where waves would go
    // jagged. A face steeper than the break slope loses some of the flow climbing it. Foam shows only on ridges,
    // where a crest folds over sharply and the normal turns suddenly, and lasts a moment, so it rides along with
    // the crest.
    //
    // Pass 0 (Splat) draws each impact WaterRippleBuffer is pressing into the force target, additively: a soft
    // pressure that pushes the water away and a drag along the impact's heading. Pass 1 (Step) advances the water
    // one fixed step.
    //
    // A blast (a bomb, the nuke) does more than press: it digs a crater (WaterCrater). While it lasts, the step's
    // BLASTS_ON variant carries the water under it outward, down the slope of a potential, so the middle empties
    // and what left it stands as a rim round the edge. The dig is fastest at its start and eases off, so the
    // crater opens at once and is then held open. All the while the water answers on its own: the rim runs off as a
    // wave, and once the dig tires the crater floods back in, overshooting into a dome where it closes.
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
        _Fade         ("Fade (seconds)",                Float) = 1
        // Smooths the flow over a neighbourhood, more at shorter wavelengths, so the grid's own jaggies die first.
        _Viscosity    ("Viscosity (world²/s)",          Float) = 0.004
        [Header(Impacts)]
        // How deep an impact of full strength presses, in rest depths.
        _Push         ("Push",                          Float) = 1.2
        // How hard an impact drags the water along its heading (a ball's wake).
        _Drag         ("Drag (world/s²)",               Float) = 6
        // An impact's press radius per world unit of its disturbance radius (read by WaterRippleBuffer).
        _PressRadius  ("Press Radius (× impact)",       Float) = 0.5
        [Header(Blasts)]
        // World units from a blast's middle to its crater's edge, where the rim starts, per world unit of the
        // blast's reported radius (read by WaterRippleBuffer). The bottom shows bare over about the inner three
        // quarters of that, so under 1 keeps a bomb's bare patch nearer half its kill radius.
        _BlastRadius  ("Crater Radius (× blast)",       Float) = 0.85
        // Rest depths a blast of strength 1 (a bomb) takes out of its middle over its seconds (read by
        // WaterRippleBuffer). The water flooding back meanwhile takes some of it away: a bomb's 3 over 0.6 s
        // leaves its middle about 2.6 down. The rim stands a third as tall. Water.shader bares the bottom below
        // its own Bare At.
        _BlastDepth   ("Crater Depth (per strength)",   Float) = 3
        // Foam a blast makes where it piles its water up, per rest depth piled: the rim, white while it rises.
        // At 3 the whole rim was one flat white ring; at 1.5 its crest is solid and the rest a lace.
        _BlastFoam    ("Rim Foam (per depth)",          Float) = 1.5
        [Header(Breakup)]
        // Height change across a texel past which a face breaks: flow climbing it loses strength.
        _BreakSlope   ("Break Slope (per texel)",       Float) = 0.15
        // How much of the flow climbing a breaking face is lost each second per unit past the break slope.
        _BreakLoss    ("Break Loss",                    Float) = 150
        // How tall a crest must stand to foam, in rest depths: above the slosh the game leaves everywhere.
        _FoamHeight   ("Foam Height",                   Float) = 0.15
        // How sharply a crest must fold over to foam: the height its middle stands above the mean of the two
        // neighbours across it, times two, in rest depths.
        _FoamFold     ("Foam Fold",                     Float) = 0.05
        // Foam a ridge makes each second per unit of fold past the foam fold.
        _FoamGain     ("Foam Gain",                     Float) = 400
        // How fast foam fades (the time constant): a moment, so it sits on the fold and moves with the crest
        // rather than trailing behind it.
        _FoamFade     ("Foam Fade (seconds)",           Float) = 0.1
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
            // On only while a blast is digging its crater (WaterRippleBuffer), so every other step pays nothing
            // for it.
            #pragma multi_compile_local __ BLASTS_ON
            #include "UnityCG.cginc"

            #define MAX_BLASTS 12

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
            float _FoamHeight;
            float _FoamFold;
            float _FoamGain;
            float _FoamFade;

            // Set by WaterRippleBuffer.
            float _StepSeconds;
            float _TexelsPerUnit;

        #ifdef BLASTS_ON
            float _BlastFoam;
            // The craters being dug: xy = the blast's middle, in texels; z = 1 over its spread squared, in texels;
            // w = its potential's scale for this step (the share of the crater this step digs).
            float4 _Blasts[MAX_BLASTS];
            float  _BlastCount;

            // The blasts' potential at a cell's middle: this step, water moves between two cells by the
            // difference of it. Computed here rather than drawn into the force target, whose half floats are too
            // coarse for it: the water moved is the small difference of two large values. Its shape
            // (WaterCrater.Potential) digs a flat-bottomed crater and piles what it dug in a rim past the edge.
            // Each blast is mirrored in the four walls and corners, so its water piles up short of a wall as if
            // thrown back by it, rather than all in the last cell.
            float BlastPotentialAt(float2 cell)
            {
                float2 size = _MainTex_TexelSize.zw;
                float total = 0.0;
                for (int i = 0; i < MAX_BLASTS && i < _BlastCount; i++)
                {
                    float4 blast = _Blasts[i];
                    float3 xs = float3(blast.x, -blast.x, 2.0 * size.x - blast.x);
                    float3 ys = float3(blast.y, -blast.y, 2.0 * size.y - blast.y);
                    float potential = 0.0;
                    for (int a = 0; a < 3; a++)
                    {
                        for (int b = 0; b < 3; b++)
                        {
                            float2 away = cell - float2(xs[a], ys[b]);
                            float u = dot(away, away) * blast.z;
                            // Five spreads out the potential is under a hundred millionth of its middle: most cells are
                            // that far from most blasts and their mirrors, and skip the exponential.
                            if (u < 25.0)
                            {
                                potential += (1.0 + u * (2.0 / 3.0 + u / 6.0)) * exp(-u);
                            }
                        }
                    }

                    total += potential * blast.w;
                }

                return total;
            }
        #endif

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
                // Only flow climbing the slope breaks: it is what would steepen the face further. Flow running
                // down it is never held back. Braked both ways, a crater's wall (steeper than the break slope from
                // top to bottom) stood for seconds as a dam the water could not run down.
                float climb = heightB - heightA;
                float breaking = flow * climb > 0.0 ? max(0.0, abs(climb) - _BreakSlope) : 0.0;
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

                // Foam rides the flow (traced back along it), fades, and grows on ridges: where the surface folds over
                // sharply across a crest standing past the foam height, so its normal turns suddenly. The fold is
                // taken in its sharpest direction (along the axes and the diagonals, the diagonals per unit length): a
                // crest line folds hard across itself and not at all along itself, which the curvature (the sum of
                // both) halves, and a broad swell folds gently every way however tall it stands. Height alone was
                // tried as the trigger and foamed the whole top of every big swell.
                float2 velocity = float2(right + left, top + bottom) * 0.5;
                float foam = tex2Dlod(_MainTex, float4(uv - velocity * _StepSeconds * _MainTex_TexelSize.xy, 0, 0)).a;
                foam *= exp(-_StepSeconds / max(_FoamFade, 0.05));
                float foldX = e.r + w.r - 2.0 * c.r;
                float foldY = n.r + s.r - 2.0 * c.r;
                float foldNE = 0.5 * (ne.r + sw.r - 2.0 * c.r);
                float foldNW = 0.5 * (nw.r + se.r - 2.0 * c.r);
                float fold = -min(min(foldX, foldY), min(foldNE, foldNW));
                float peak = c.r > _FoamHeight ? max(0.0, fold - _FoamFold) : 0.0;
                foam += _StepSeconds * _FoamGain * peak;

            #ifdef BLASTS_ON
                // A blast carries water across each edge by the difference of its potential (the same for both
                // cells on the edge, and nothing across a wall, so none is made or lost), whatever the flow is
                // doing: the crater is dug in the blast's own time, and only then does the water answer. Where it
                // piles the water up, the rim, it foams.
                float2 middle = floor(cell) + 0.5;
                float liftC = BlastPotentialAt(middle);
                float thrown = (liftC - BlastPotentialAt(middle + float2(1.0, 0.0))) * openRight
                             + (liftC - BlastPotentialAt(middle - float2(1.0, 0.0))) * openLeft
                             + (liftC - BlastPotentialAt(middle + float2(0.0, 1.0))) * openTop
                             + (liftC - BlastPotentialAt(middle - float2(0.0, 1.0))) * openBottom;
                height -= thrown;
                foam += _BlastFoam * max(0.0, -thrown);
            #endif

                // The clamp is only a guard against a runaway: a crater dug in a corner (twice mirrored) reaches
                // about 6 rest depths down, and clamping what the water really reaches would make or lose water.
                return float4(clamp(height, -8.0, 8.0), right, top, saturate(foam));
            }
            ENDCG
        }
    }
}
