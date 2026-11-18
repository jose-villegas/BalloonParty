Shader "BalloonParty/Sprite/ShapeInk"
{
    // The ink of a score shape (ShapeInkView), drawn on the shape's baked wire mesh (ShapeInkMeshBuilder). The
    // mesh is every closed walk of the shape as a ribbon, still in the shape's own space, and each point of it
    // knows how far along its walk it is. Pens are not objects: a walk of perimeter P carries k pens P/k apart,
    // all moved on by the same distance since the launch, so from that one number each pixel knows how far
    // behind the last pen to pass it sits. That distance is the ink's age, and the ink's width, fade and colour
    // are read from it (the ink ramp).
    // The pen itself is the stroke's own rounded end. Each segment of the mesh carries a spare square for it,
    // which the vertex stage moves to wherever on that segment a pen is, or folds away while none is there.
    // Ribbon and pen face the camera and keep their size in world units however the shape is scaled.
    // A shape is drawn three times, by three materials. First its drop shadow (_InkShadow 1): the whole ink
    // again, moved away from the scene's light as the game's other drop shadows are, dark, soft-edged and
    // fading with the light. The stencil lets each pixel take the shadow once, so strokes that cross or lie
    // on one another cast one even shadow, not a darker patch. Then the half of the shape behind its centre
    // (_InkHalf -1), then the half in front (_InkHalf 1). The shapes are wire frames of rounded solids, so
    // that alone puts the near side over the far side.
    //
    // The look is a pen on paper: a hand-drawn stroke, after the sketchy edges of a drafting program. The line
    // wavers and its pressure wavers with it, its edge is ragged and paper specks show through (the heads-ups'
    // pencil noise, laid along the stroke so the grain stays with the line as the shape tumbles), and ink pools
    // darker at its edges. It can be darker while wet (_PenWet.x; not as shipped, where a darker head reads as
    // a dot riding the line). A straight edge runs on a little past each corner, as a quick hand does, and a
    // second, fainter stroke rides beside the first (_Sketch.x, 0 for none). The edges on the far side of the
    // shape are drawn as hidden edges are: lighter and thinner, and dashed if asked (_Far.z; unbroken as
    // shipped). The ink takes the scene's light (SceneLight.cginc) as the gameplay surfaces do: the time of day
    // tints and dims it, part-way, and a light passing close (a spark, a laser, lightning) colours and
    // brightens it, never past white.
    Properties
    {
        _InkRamp ("Ink Ramp (R width over the widest, G alpha, B tail mix)", 2D) = "white" {}
        _TailColor ("Tail Color", Color) = (0.95, 0.95, 0.91, 1)
        _InkHalf ("Half drawn (-1 behind the centre, 1 in front, 0 both)", Float) = 0

        [Header(Drop shadow)]
        _InkShadow ("Drawn as the drop shadow (set by ShapeInkPool)", Float) = 0
        _ShadowColor ("Shadow Color", Color) = (0.08, 0.1, 0.18, 1)
        _Shadow ("Shadow: distance from the ink in world units (x), opacity (y), edge softness (z), width over the ink's (w)", Vector) = (0.035, 0.45, 0.85, 1.5)
        // The shadow's stencil, set by ShapeInkPool: it writes and tests a bit of its own, the ink ignores it.
        [HideInInspector] _ShadowStencilComp ("Shadow Stencil Comp", Float) = 8
        [HideInInspector] _ShadowStencilPass ("Shadow Stencil Pass", Float) = 0

        [Header(Pen on paper)]
        [NoScaleOffset] _PencilNoiseTex ("Pencil Noise (edge RGB, grain A)", 2D) = "black" {}
        _PenEdge ("Edge: roughness (x), softness (y), tiles per unit along (z), pooling darkness (w)", Vector) = (0.35, 0.12, 2.5, 0.18)
        _PenGrain ("Grain: paper showing through (x), tiles per unit along (y), tiles per world unit across (z)", Vector) = (0.45, 2, 6, 0)
        _PenHand ("Hand: waver in world units (x), wavers per unit (y), pressure on width (z), pressure changes per unit (w)", Vector) = (0.012, 0.35, 0.25, 0.6)
        _PenWet ("Wet ink: darkness, 1 for none (x), how far along the ink it dries (y), pressure on opacity (z), the rounded end's width over the stroke's (w)", Vector) = (1, 0.12, 0.2, 1)
        _Sketch ("Sketch: second stroke strength, 0 for none (x), its offset in world units (y), run past a corner in world units (z)", Vector) = (0.45, 0.022, 0.09, 0)
        _Far ("Far side: opacity (x), width (y), dashes per unit, 0 for none (z), soft band (w)", Vector) = (0.5, 0.75, 0, 0.3)
        _PenLight ("Light: how much of the scene light the ink takes (x), how much of the time of day's colour (y) and of its darkening (z)", Vector) = (1, 0.4, 0.4, 0)
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        // One bit, well clear of the bits UI masks take, marking the pixels a shadow has already darkened.
        Stencil
        {
            Ref 128
            ReadMask 128
            WriteMask 128
            Comp [_ShadowStencilComp]
            Pass [_ShadowStencilPass]
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            // A corner turning more than this (20 degrees) is a sharp one: its miter would run off to a spike,
            // so the ribbon ends square there and the stroke runs on past the corner.
            #define MITER_MIN_COS 0.94
            // The longest a miter may reach, in ribbon half-widths.
            #define MITER_MAX 2.0
            // Must match ShapeInkRamp.Size.
            #define RAMP_SIZE 128.0

            sampler2D _InkRamp;
            sampler2D _PencilNoiseTex;
            half4 _TailColor;
            float _InkHalf;
            float _InkShadow;
            half4 _ShadowColor;
            float4 _Shadow;
            float4 _PenEdge;
            float4 _PenGrain;
            float4 _PenHand;
            float4 _PenWet;
            float4 _Sketch;
            float4 _Far;
            float4 _PenLight;

            // From ShapeInkView, per shape. _InkPen: the distance every pen has moved since the launch, in the
            // mesh's own units (x), the ink's length as a share of the gap between two pens (y), world units
            // per mesh unit (z) and the snap fade (w). _InkSize: the mesh ribbon's half-width (x), the ink's
            // half-width at its widest (y) and at the pen (z), in world units. _InkWave: the waving sphere's
            // amplitude (x, 0 for every other shape) and its clock (y).
            half4 _InkColor;
            float4 _InkPen;
            float4 _InkSize;
            float4 _InkWave;

            // What ShapeInkMeshBuilder writes. A ribbon vertex (kind 0) sits on its end of the segment; a pen's
            // vertex (kind 1, 2, ...: the first, second, ... pen on the segment) sits on the near end.
            struct appdata_t
            {
                float4 vertex    : POSITION;
                // (distance along the walk here, gap between two pens, the walk's index, kind)
                float4 walk      : TEXCOORD0;
                // The other end of the segment, and the side of the ribbon or of the square (w, -1 or 1).
                float4 other     : TEXCOORD1;
                // Ribbon: the walk's next point past this end, and whether this is the far end (w).
                // Pen: the far end's distance along the walk (x) and the square's corner along the segment (y).
                float4 neighbour : TEXCOORD2;
                // Ribbon: the other end's distance along the walk (x).
                float4 extra     : TEXCOORD3;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // (the segment's near end's distance along the walk, its far end's, gap between two pens,
                // kind: 0 ribbon, 1 pen)
                float4 ink    : TEXCOORD0;
                // How far in front of the shape's centre the point is, toward the camera.
                float toward  : TEXCOORD1;
                // Ribbon: (distance from the segment's near end along it, the segment's length, signed distance
                // across it, the walk's seed for the hand's noise). Pen: where in the square the pixel is, from
                // its middle, along the segment (x) and across it (z). All lengths in world units.
                float4 span   : TEXCOORD2;
                // Where the point is in the world, for the scene light there.
                float2 world  : TEXCOORD3;
            };

            float Toward(float3 world, float3 viewAxis)
            {
                // The view axis has no part in the screen's plane, so a shadow's shift leaves this as it was.
                return dot(world - unity_ObjectToWorld._m03_m13_m23, viewAxis);
            }

            // A point of the mesh in the world. The drop shadow is the same shape set down away from the light.
            float3 InWorld(float3 local)
            {
                float3 world = mul(unity_ObjectToWorld, float4(local, 1.0)).xyz;
                world.xy -= SceneLightDirection() * (_Shadow.x * _InkShadow);
                return world;
            }

            // The waving sphere's swell, along its own radius: three sines along directions that don't line up,
            // so it undulates without visibly repeating. _InkWave.x is 0 for every other shape.
            float3 Wave(float3 p)
            {
                float t = _InkWave.y;
                float phase1 = dot(p, float3(0.8, 0.4, 0.2)) * 4.5 + t * 4.0;
                float phase2 = dot(p, float3(-0.3, 0.9, 0.5)) * 3.7 + t * 3.2;
                float phase3 = dot(p, float3(0.5, -0.6, 0.8)) * 5.2 + t * 4.8;
                float displacement = (sin(phase1) + 0.6 * sin(phase2) + 0.35 * sin(phase3)) * _InkWave.x;
                return p + p * displacement;
            }

            float3 OnViewPlane(float3 v, float3 viewAxis)
            {
                return v - viewAxis * dot(v, viewAxis);
            }

            float3 DirectionOr(float3 v, float3 fallback)
            {
                float size = length(v);
                return size > 1e-5 ? v / size : fallback;
            }

            // The walk's own place in the hand's noise, so two walks don't waver alike.
            float Seed(float walk)
            {
                return frac(walk * 0.61803 + 0.137);
            }

            v2f Ribbon(appdata_t IN, float3 here, float3 other, float3 viewAxis, float3 fallback)
            {
                v2f OUT;
                float3 neighbour = InWorld(Wave(IN.neighbour.xyz));
                float side = IN.other.w;
                float farEnd = IN.neighbour.w;

                // Both run the way the pens travel: along this segment, and along the one that meets it here.
                float3 segment = OnViewPlane(farEnd > 0.5 ? here - other : other - here, viewAxis);
                float segmentLength = length(segment);
                float3 along = DirectionOr(segment, fallback);
                float3 meeting = DirectionOr(
                    OnViewPlane(farEnd > 0.5 ? neighbour - here : here - neighbour, viewAxis), along);

                float3 normal = cross(viewAxis, along);
                float3 meetingNormal = cross(viewAxis, meeting);
                float3 offset = normal;
                float past = 0.0;
                if (dot(along, meeting) > MITER_MIN_COS)
                {
                    // Both segments work out the same miter here, so the corner closes.
                    float3 miter = normalize(normal + meetingNormal);
                    offset = miter / max(dot(miter, normal), 1.0 / MITER_MAX);
                }
                else
                {
                    // A sharp corner: a pen on paper runs on past it.
                    past = _Sketch.z * _InkPen.w;
                }

                OUT.toward = Toward(here, viewAxis);
                here += along * (farEnd > 0.5 ? past : -past) + offset * side * _InkSize.x;

                OUT.vertex = mul(UNITY_MATRIX_VP, float4(here, 1.0));
                OUT.world = here.xy;
                OUT.ink = farEnd > 0.5
                    ? float4(IN.extra.x, IN.walk.x, IN.walk.y, 0.0)
                    : float4(IN.walk.x, IN.extra.x, IN.walk.y, 0.0);
                OUT.span = float4(farEnd > 0.5 ? segmentLength + past : -past, segmentLength, side * _InkSize.x,
                    Seed(IN.walk.z));
                return OUT;
            }

            // A square of half-size reach round centre, lying along the segment and facing the camera. where is
            // the distance along the walk of the point it sits on.
            v2f Square(appdata_t IN, float3 centre, float3 along, float3 viewAxis, float reach, float kind, float where)
            {
                v2f OUT;
                float2 corner = float2(IN.neighbour.y, IN.other.w) * reach;
                OUT.toward = Toward(centre, viewAxis);
                centre += along * corner.x + cross(viewAxis, along) * corner.y;
                OUT.vertex = mul(UNITY_MATRIX_VP, float4(centre, 1.0));
                OUT.world = centre.xy;
                OUT.ink = float4(where, where, IN.walk.y, kind);
                OUT.span = float4(corner.x, 0.0, corner.y, Seed(IN.walk.z));
                return OUT;
            }

            v2f PenEnd(appdata_t IN, float3 here, float3 other, float3 viewAxis, float3 fallback)
            {
                float start = IN.walk.x;
                float gap = IN.walk.y;
                float end = IN.neighbour.x;

                // The first pen at or past the segment's near end, then the ones behind it, a gap apart each.
                float toPen = _InkPen.x - start;
                toPen -= gap * floor(toPen / gap);
                float pen = start + toPen + (IN.walk.w - 1.0) * gap;
                float present = step(pen, end - 1e-5);

                float3 along = DirectionOr(OnViewPlane(other - here, viewAxis), fallback);
                float3 centre = lerp(here, other, saturate((pen - start) / max(end - start, 1e-6)));

                // Folded onto the pen's point while no pen is on this segment: a square of no size draws nothing.
                // The whole square goes with the half its pen is in.
                float reach = _InkSize.z * _InkPen.w * _PenWet.w + abs(_PenHand.x) + 0.02;
                return Square(IN, centre, along, viewAxis, reach * present, 1.0, pen);
            }

            v2f vert(appdata_t IN)
            {
                float3 here = InWorld(Wave(IN.vertex.xyz));
                float3 other = InWorld(Wave(IN.other.xyz));
                float3 viewAxis = normalize(UNITY_MATRIX_V[2].xyz);
                float3 fallback = normalize(UNITY_MATRIX_V[0].xyz);

                if (IN.walk.w > 0.5)
                {
                    return PenEnd(IN, here, other, viewAxis, fallback);
                }

                return Ribbon(IN, here, other, viewAxis, fallback);
            }

            half3 Ramp(float u)
            {
                float rampU = (saturate(u) * (RAMP_SIZE - 1.0) + 0.5) / RAMP_SIZE;
                return tex2Dlod(_InkRamp, float4(rampU, 0.5, 0, 0)).rgb;
            }

            // 1 on the near side of the shape, 0 on the far side, easing across its middle.
            float Front(float toward)
            {
                float band = max(_Far.w, 1e-3);
                return smoothstep(-band, band, toward / max(_InkPen.z, 1e-4));
            }

            // The ink's colour as old as u: darker while wet, then drying toward the tail's colour.
            half3 PenColour(float u, half3 ramp)
            {
                half3 colour = lerp(_InkColor.rgb, _TailColor.rgb, ramp.b);
                return colour * lerp(_PenWet.x, 1.0, smoothstep(0.0, _PenWet.y, u));
            }

            // The hand at a distance along a walk: how far off its line the stroke has wavered there, in world
            // units (x), and how lightly it presses, 0 full pressure to 1 lightest (y).
            float2 Hand(float along, float seed)
            {
                float waver = tex2Dlod(_PencilNoiseTex, float4(along * _PenHand.y + seed, seed * 1.7, 0, 0)).r;
                float pressure = tex2Dlod(_PencilNoiseTex, float4(along * _PenHand.w + seed * 0.3, seed + 0.37, 0, 0)).g;
                return float2((waver - 0.5) * 2.0 * _PenHand.x, pressure);
            }

            // One stroke of the pen, premultiplied. along is the distance along the walk and across the signed
            // distance from the walk's line; run is how far past a corner the stroke has run on, 0 to 1.
            half4 Stroke(float along, float across, float run, float behind, float ribbon, float travelled,
                float seed, float pixel, float front)
            {
                float u = behind / ribbon;
                half3 ramp = Ramp(u);

                // The hand: the line wavers off its course, and presses harder and lighter along it.
                float2 hand = Hand(along, seed);
                float pressure = hand.y;
                across -= hand.x;

                float halfWidth = ramp.r * _InkSize.y * _InkPen.w * lerp(_Far.y, 1.0, front);
                halfWidth *= lerp(1.0, _Shadow.w, _InkShadow);
                halfWidth *= 1.0 - _PenHand.z * pressure;
                // Running past a corner, the pen lifts: the stroke thins to nothing.
                halfWidth = max(halfWidth * sqrt(1.0 - run), 1e-4);

                // Where the edge noise is high the ink stops short of the stroke's edge.
                float fromCentre = abs(across) / halfWidth;
                float2 edgeUV = float2(along * _PenEdge.z + seed, across / halfWidth * 0.25 + seed * 2.3);
                float reach = 1.0 - _PenEdge.x * tex2Dlod(_PencilNoiseTex, float4(edgeUV, 0, 0)).r;
                float soft = max(_PenEdge.y + _Shadow.z * _InkShadow, pixel / halfWidth);
                float coverage = 1.0 - smoothstep(reach - soft, reach, fromCentre);

                float2 grainUV = float2(along * _PenGrain.y + seed * 3.1, across * _PenGrain.z);
                float speck = tex2Dlod(_PencilNoiseTex, float4(grainUV, 0, 0)).a;

                // Hidden edges: lighter, and dashed if asked.
                float shown = lerp(_Far.x, 1.0, front);
                float dash = step(0.5, frac(along * _Far.z));
                shown *= lerp(lerp(1.0, dash, step(1e-3, _Far.z)), 1.0, front);

                // Nothing past the ink's length, and nothing the pen has not reached yet.
                float drawn = step(behind, ribbon) * step(behind, travelled);

                half4 c;
                // A shadow is cast by the stroke's shape, not by the paper's grain in it.
                float grain = 1.0 - _PenGrain.x * speck * (1.0 - _InkShadow);
                c.a = ramp.g * _InkColor.a * coverage * grain * (1.0 - _PenWet.z * pressure)
                    * (1.0 - run) * shown * drawn;

                // Ink pools at the stroke's edges.
                c.rgb = PenColour(u, ramp) * (1.0 - _PenEdge.w * smoothstep(0.55, 1.0, fromCentre / reach)) * c.a;
                return c;
            }

            half4 Pen(v2f IN, float along, float behind, float ribbon, float travelled)
            {
                float front = Front(IN.toward);
                float2 local = IN.span.xz;
                float across = IN.span.z;
                float seed = IN.span.w;
                float pixel = max(fwidth(across), 1e-5);
                float discPixel = max(fwidth(local.x) + fwidth(local.y), 1e-5) * 0.5;

                half4 result;
                if (IN.ink.w > 0.5)
                {
                    // The pen: the stroke's own rounded end. It sits where the stroke has wavered to at the pen,
                    // is as wide as the stroke is pressed there, and is the same wet ink. Only the half in front
                    // of the pen is drawn: the half behind would lie over the stroke's start and darken it into
                    // a dot on the line.
                    half3 ramp = Ramp(0.0);
                    float2 hand = Hand(along, seed);
                    float radius = _InkSize.z * _InkPen.w * lerp(_Far.y, 1.0, front) * (1.0 - _PenHand.z * hand.y)
                        * _PenWet.w;
                    float distance = length(local - float2(0.0, hand.x));
                    float inside = saturate((radius - distance) / discPixel + 0.5) * saturate(local.x / discPixel + 0.5);
                    float alpha = inside * ramp.g * _InkColor.a * (1.0 - _PenWet.z * hand.y) * lerp(_Far.x, 1.0, front);
                    result = half4(PenColour(0.0, ramp) * alpha, alpha);
                }
                else
                {
                    float beyond = max(max(-IN.span.x, IN.span.x - IN.span.y), 0.0);
                    float run = saturate(beyond / max(_Sketch.z * _InkPen.w, 1e-5));

                    result = Stroke(along, across, run, behind, ribbon, travelled, seed, pixel, front);
                    if (_Sketch.x > 1e-3)
                    {
                        // The second stroke: beside the first, wavering its own way, and fainter.
                        half4 second = Stroke(along, across - _Sketch.y, run, behind, ribbon, travelled, seed + 0.53,
                            pixel, front) * _Sketch.x;
                        result = result + second * (1.0 - result.a);
                    }
                }

                return result;
            }

            half4 frag(v2f IN) : SV_Target
            {
                clip(IN.toward * _InkHalf);

                // Where along the walk the pixel is: on the segment, or held at its end past a corner.
                float along = lerp(IN.ink.x, IN.ink.y, saturate(IN.span.x / max(IN.span.y, 1e-5)));
                float gap = IN.ink.z;
                float travelled = _InkPen.x;
                float ribbon = gap * _InkPen.y;

                // How far behind the last pen to pass it this pixel sits.
                float behind = travelled - along;
                behind -= gap * floor(behind / gap);

                // Read outside the branches below: a light-field tap wants its screen-space gradients.
                float3 light = SceneLightTintAt(IN.world) + SceneLightAmbientRegrade(_PenLight.y, _PenLight.z);

                half4 ink = Pen(IN, along, behind, ribbon, travelled);
                if (_InkShadow > 0.5)
                {
                    // The ink's shape, in the shadow's colour: no light, no shadow.
                    float shade = ink.a * _Shadow.y * _ShadowColor.a * ShadowLightFade();
                    return half4(_ShadowColor.rgb * shade, shade);
                }

                // Lit, then scaled back, hue kept, until no channel passes the ink's own opacity: the ink is
                // premultiplied, and an ink brighter than white would bloom.
                half3 lit = ink.rgb * light;
                half peak = max(lit.r, max(lit.g, lit.b)) / max(ink.a, 1e-4);
                lit *= min(1.0, 1.0 / max(peak, 1e-3));
                ink.rgb = lerp(ink.rgb, lit, _PenLight.x);
                return ink;
            }
            ENDCG
        }
    }
}
