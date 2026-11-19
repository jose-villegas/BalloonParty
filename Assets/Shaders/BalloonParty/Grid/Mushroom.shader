Shader "BalloonParty/Grid/Mushroom"
{
    // The meadow's mushroom (PLAN-MeadowMushrooms), seen from above as the board is: what shows is its cap, a
    // dome that fills the circle the ball bounces off (_Radius, the mushroom's own radius from its settings).
    // What tells it from a ball or a balloon is a toadstool's own marking: a darker crown, ragged pale warts
    // scattered round it, and a paler margin pleated over the gills. The stem stands under the middle of the cap
    // and is hidden by it until a hit. Drawn from the quad's own position, as the nuke, the disco ball and the
    // golden balloon are; no texture is read.
    // It takes the scene light as the balloons do: sampled once at its centre, the time-of-day part re-graded
    // (_AmbientHue, _AmbientDimming) so it keeps its colour at night, capped at white so it never blooms.
    //
    // Its shadow is drawn here too, not by a baked child: the same shape again, moved down-light as the balloons'
    // shadows are and soft at the edge, and a dark ring where the cap overhangs the ground. So the shadow follows
    // the cap, and the mushroom and its shadow fade together with the meadow through a mist reveal, as the bushes
    // do (GroundSceneryAt).
    //
    // MushroomView pushes the hit's reaction per renderer. _Lean, an offset in the quad's own units, is how far
    // the hit has knocked the cap off its foot. The foot stays, so the stem is drawn from the foot to under the
    // cap; and the cap tips into the lean (_TiltPerLean, easing toward CAP_MAX_TILT), which from above narrows it
    // along the lean and turns its lit side. Narrowed and moved off, it uncovers the stem behind it: the stem
    // shows only while the cap wobbles, on the side the ball came from, which is how a knock's direction reads. _Pulse swells the cap and lights its spots for the moment a chain pays the lance: the one part
    // that glows past white. _Seed turns the spots about the cap so no two mushrooms are spotted alike.
    //
    // A lance cuts the cap (PLAN-MeadowMushrooms, "The lance cuts caps"). MushroomView pushes that too. While
    // _CutFall.x is above 0 the cap is drawn as its two pieces either side of the cut line (_CutLine), each moved
    // off the line, carried along the lance's way, turned and shrunk as it falls (_CutPieces), fading, a band of
    // pale flesh along its cut edge. _CapGrow is the whole cap's size: 0 while it is cut, when what is left is the
    // stem seen from above (its flat pale top, a darker rim where the cap sat, a short shadow), and back to 1 as
    // the cap regrows. With no cut the pieces' work is skipped on a uniform, so a whole cap costs what it did.
    //
    // A blast blows the cap off whole ("Blasts take caps off"). It is drawn by the same means, as the one piece of
    // a cut that misses the cap: the view lays the line behind the cap, across the way it is thrown, so the piece
    // on its far side is the whole cap, moved off the line (thrown), turned, larger while it is lifted and smaller
    // as it lands (_CutPieces), its shadow further off it while it is up (_CutFall.y over 1). _CutFall.z set says
    // there is one piece, and the other is not worked out.

    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Cap)]
        _CapLit    ("Lit",   Color) = (1.00, 0.55, 0.12, 1)
        _CapShade  ("Shade", Color) = (0.80, 0.30, 0.05, 1)
        _CapEdge   ("Edge",  Color) = (0.50, 0.17, 0.04, 1)
        _EdgePower ("Edge Falloff", Range(1, 24)) = 10
        // How round the dome is lit: 1 a half ball whose edge faces sideways, less a flatter cap.
        _DomeCurve ("Dome Curve", Range(0.3, 1)) = 0.82
        // The crown: a darker patch in the middle of the cap, as a toadstool's has. Its alpha is how strong.
        _CrownColor ("Crown", Color) = (0.74, 0.17, 0.03, 0.7)
        _CrownSize  ("Crown Size", Range(0.1, 0.9)) = 0.52
        // The margin: a paler band round the rim, pleated. Its alpha is how strong; the band starts this far
        // out from the middle. A ball has neither a crown nor a margin, which is what tells the cap from one.
        _MarginColor ("Margin", Color) = (1.00, 0.74, 0.30, 0.85)
        _MarginStart ("Margin Start", Range(0.4, 0.95)) = 0.70
        // The pleats in the margin: how many, and how dark.
        _PleatCount ("Pleat Count", Range(8, 80)) = 40
        _Pleats     ("Pleats", Range(0, 1)) = 0.6
        _SpotColor ("Spots", Color) = (1.00, 0.97, 0.88, 1)
        // How much larger or smaller every spot is drawn, and how ragged its outline is.
        _SpotSize  ("Spot Size", Range(0.5, 1.5)) = 0.92
        _SpotLumps ("Spot Raggedness", Range(0, 0.6)) = 0.45

        [Header(Stem)]
        _StemLit   ("Stem Lit", Color) = (1.00, 0.95, 0.84, 1)
        _StemShade ("Stem Shade", Color) = (0.74, 0.62, 0.48, 1)
        // The shade the cap throws on the stem where it runs in under it.
        _CapOcclusion ("Shade Under the Cap", Range(0, 1)) = 0.45

        [Header(Light)]
        // The light's height over the board: low rakes across the cap, high lights it face-on.
        _LightHeight    ("Light Height", Range(0.1, 3)) = 0.9
        _SheenPower     ("Sheen Sharpness", Range(2, 64)) = 9
        _SheenIntensity ("Sheen Intensity", Range(0, 1)) = 0.2
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
        _AmbientHue     ("Ambient Hue", Range(0, 1)) = 0.25
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 0.4

        [Header(Shadow)]
        // Lit by the scene light, like the ground it falls on; its alpha is how dark the cast shadow is.
        _ShadowColor    ("Color", Color) = (0.03, 0.06, 0.03, 0.7)
        // How far down-light of the mushroom its shadow lies, in the quad's own units, as the balloons' baked
        // shadows lie 0.1 from theirs.
        _ShadowDistance ("Down-Light Offset", Range(0, 0.3)) = 0.1
        // How far either side of the outline the shadow's edge fades, in the quad's own units.
        _ShadowSoftness ("Softness", Range(0.005, 0.15)) = 0.05
        // The disc the light's direction is averaged over, so a light passing close swings the shadow smoothly.
        _ShadowReceiverRadius ("Receiver Radius", Range(0, 1)) = 0.4
        // The dark on the ground under the cap's overhang, which a cap knocked aside uncovers.
        _FootShadow     ("Shade Under the Overhang", Range(0, 1)) = 0.5

        [Header(Reaction)]
        // Radians the cap tips for a lean of one radius, while the lean is small: it eases toward the most it
        // may tip (CAP_MAX_TILT) and never passes it.
        _TiltPerLean    ("Cap Tilt per Lean", Range(0, 6)) = 3.4
        // How much the cap swells at a full pulse, and how far past white its spots shine then.
        _PulseSwell     ("Pulse Swell", Range(0, 0.6)) = 0.22
        _PulseGlow      ("Pulse Glow (HDR)", Range(0, 6)) = 1.2

        [Header(Cut)]
        // The cap's flesh: a band along the cut edge of each piece (this wide, as a share of the radius), and the
        // top of the stem once the cap is gone.
        _FleshColor ("Flesh", Color) = (1.00, 0.93, 0.76, 1)
        _FleshBand  ("Cut Edge Band", Range(0, 0.3)) = 0.1
        // The cut stalk seen from above, once the cap is gone: quieter than the cap's flesh, so a stem nothing
        // bounces off sits in the grass. Its fibres run out from a darker, hollow middle.
        _StumpColor ("Cut Stalk", Color) = (0.70, 0.60, 0.43, 1)
        _StumpShade ("Cut Stalk Shade", Color) = (0.40, 0.31, 0.21, 1)
        _StumpGrain ("Stalk Fibres", Range(0, 0.6)) = 0.3

        [Header(Night Glow)]
        // At night the warts shine past white and bloom, the one part of the mushroom that does (the owner's
        // choice: nothing else that stands on the board blooms). How far past white at full night, and its tint.
        _NightGlow ("Night Glow (HDR)", Range(0, 6)) = 1.6
        _NightGlowTint ("Night Glow Tint", Color) = (1.00, 0.95, 0.80, 1)
        // How dark the time of day's light has to be for the glow to start and to be full: 0 is noon, the
        // shipped dusk is 0.33 and the shipped night 0.62.
        _NightGlowFrom ("Glow Starts at Dark", Range(0, 1)) = 0.25
        _NightGlowFull ("Glow Full at Dark", Range(0, 1)) = 0.6

        [Header(Roots)]
        // The rootlets that run out from the foot over the ground, under the cap. Lit as the ground is. The
        // alpha is how solid they are.
        _RootColor ("Roots", Color) = (0.52, 0.38, 0.23, 1)
        // A root's half width at the foot, as a share of the radius. It tapers to nothing at its tip.
        _RootWidth ("Root Width", Range(0, 0.15)) = 0.085
        // The hint of shade a root lies in, which lifts it off the grass.
        _RootShade ("Root Shade", Range(0, 1)) = 0.45
        // How far a root's two branches part, as a share of the room between two roots.
        _RootFork  ("Root Fork", Range(0, 0.5)) = 0.3
        // How much a root bends on its way out, in radians a radius.
        _RootBend  ("Root Bend", Range(0, 1)) = 0.35

        // The mushroom's radius in the quad's own units. MushroomView pushes the settings' value; this is what
        // an unplaced prefab previews with.
        _Radius ("Radius", Float) = 0.3
        [HideInInspector] _Lean  ("Lean", Vector) = (0, 0, 0, 0)
        [HideInInspector] _Pulse ("Pulse", Float) = 0
        [HideInInspector] _Seed  ("Seed", Float) = 0
        // The whole cap's size: 1 whole, 0 cut away.
        [HideInInspector] _CapGrow   ("Cap Grow", Float) = 1
        // The cut, in the quad's own units: xy a point of the lance's line, zw its way (a unit direction).
        [HideInInspector] _CutLine   ("Cut Line", Vector) = (0, 0, 1, 0)
        // The lean the cap had when it was cut: where its pieces start from.
        [HideInInspector] _CutLean   ("Cut Lean", Vector) = (0, 0, 1, 0)
        // The pieces: x how far each has moved off the line, y how far along the lance's way (quad units), z how
        // far each has turned (radians, one each way), w their size.
        [HideInInspector] _CutPieces ("Cut Pieces", Vector) = (0, 0, 0, 1)
        // x how much of the pieces still shows (0: none, and none of their work is done), y how high they still
        // are (1 at the cap's height, 0 on the ground, where their shadows have closed in under them).
        [HideInInspector] _CutFall   ("Cut Fall", Vector) = (0, 1, 0, 0)
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
            // The light is read in the vertex stage (tex2Dlod).
            #pragma target 3.5

            #include "UnityCG.cginc"
            #include "../Include/MathConst.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/Ground.cginc"

            // The shape, in units of the mushroom's radius, about the slot's centre, where the stem stands.
            // The cap at rest is the circle the ball bounces off.
            #define CAP_RADIUS       1.0
            // The cap never tips further than this (radians): past it, it would be seen near edge-on, a sliver.
            #define CAP_MAX_TILT     0.9
            // How far round the dome the cap's rim is, for placing the spots: under 1, so one near the rim is a
            // sliver and not a line.
            #define SPOT_CURVE       0.96
            // The stem: from its foot, at the centre, to this share of the way to the cap's middle, slimmer there.
            #define STEM_FOOT_RADIUS 0.60
            #define STEM_TOP_RADIUS  0.44
            #define STEM_REACH       0.8
            // With the cap cut away the stem is seen from above: its flat top, this wide, inside a flank this wide,
            // smaller than the foot the knocked cap uncovers. It stands lower than the cap did, so its shadow lies
            // this share of the cap's away.
            #define STUMP_RADIUS     0.50
            #define STUMP_TOP_RADIUS 0.36
            #define STUMP_SHADOW     0.4
            // The stalk's edge is torn, not turned: it is eaten in by up to this much, unevenly. Its top carries
            // this many fibres, and the skirt round it this many remnants of the gills.
            #define STUMP_TEAR       0.09
            #define STUMP_FIBRES     13.0
            #define STUMP_GILLS      21.0
            // Each piece of a cut cap turns about a point this far to its own side of the cut, and shrinks, as it
            // falls, toward its own far rim: so its cut edge draws back off the stem.
            #define PIECE_PIVOT      0.4
            // A piece's shadow lies at most this many times as far off it as the cap's at rest does: more than
            // once only for a cap a blast has lifted.
            #define PIECE_SHADOW_MOST 4.0
            // The roots: this many run out from the foot, to no further than this (just inside the cap's rim at
            // rest, so a whole cap hides them), each forking where it is this share of its length out, and again.
            #define ROOT_COUNT       6.0
            #define ROOT_REACH       0.96
            #define ROOT_FORK_AT     0.60
            #define ROOT_FORK_AGAIN  0.82
            // The dark under the overhang reaches a little past the cap at rest.
            #define FOOT_SHADE_RADIUS 1.12
            #define SPOT_COUNT       18
            // Room the quad keeps past what it draws, in radii: for an edge's anti-aliasing (a pixel is a twelfth
            // of a radius in the GI's small scene capture), and past the shadow's soft edge as a share of its
            // softness, since the ellipse's distance is near enough and not exact.
            #define QUAD_EDGE_ROOM   0.1
            #define QUAD_SHADOW_ROOM 1.25

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                // rgb the tint; a how much of the mushroom shows through a mist reveal.
                half4  color     : COLOR;
                // xy = in mushroom radii from the slot's centre; zw = cos and sin of the spots' turn about the cap.
                float4 position  : TEXCOORD0;
                // xy = the cap's middle, in radii; zw = the way it leans, a unit direction.
                float4 cap       : TEXCOORD1;
                // xy = cos and sin of the cap's tilt, z = the cap's radius with its swell.
                float3 tilt      : TEXCOORD2;
                // The light is sampled once, at the mushroom's centre, so these are the same at every vertex.
                float3 toLight   : TEXCOORD3;
                // w = how far past white the warts shine, which is by how dark the time of day is.
                float4 halfway   : TEXCOORD4;
                float3 lightTint : TEXCOORD5;
                // The ground's light, without the re-grade the body takes.
                float3 groundTint : TEXCOORD6;
                // xy = where the shadow lies off the mushroom, in radii; z = how dark it is.
                float3 shadow    : TEXCOORD7;
                // The cap as it was when it was cut, which its pieces are pieces of: as cap and tilt above.
                float4 cutCap    : TEXCOORD8;
                float3 cutTilt   : TEXCOORD9;
            };

            fixed4 _Color;

            half4  _CapLit;
            half4  _CapShade;
            half4  _CapEdge;
            float  _EdgePower;
            float  _DomeCurve;
            half4  _CrownColor;
            float  _CrownSize;
            half4  _MarginColor;
            float  _MarginStart;
            float  _PleatCount;
            float  _Pleats;
            half4  _SpotColor;
            float  _SpotSize;
            float  _SpotLumps;

            half4  _StemLit;
            half4  _StemShade;
            float  _CapOcclusion;

            float  _LightHeight;
            float  _SheenPower;
            float  _SheenIntensity;
            float  _LightInfluence;
            float  _AmbientHue;
            float  _AmbientDimming;

            half4  _ShadowColor;
            float  _ShadowDistance;
            float  _ShadowSoftness;
            float  _ShadowReceiverRadius;
            float  _FootShadow;

            float  _TiltPerLean;
            float  _PulseSwell;
            float  _PulseGlow;

            half4  _FleshColor;
            float  _FleshBand;
            half4  _StumpColor;
            half4  _StumpShade;
            float  _StumpGrain;
            half4  _RootColor;
            float  _RootWidth;
            float  _RootFork;
            float  _RootBend;
            float  _RootShade;
            float  _NightGlow;
            half4  _NightGlowTint;
            float  _NightGlowFrom;
            float  _NightGlowFull;

            float  _Radius;
            float4 _Lean;
            float  _Pulse;
            float  _Seed;
            float  _CapGrow;
            float4 _CutLine;
            float4 _CutLean;
            float4 _CutPieces;
            float4 _CutFall;

            // The spots, a toadstool's warts: where each sits on the cap's dome (a unit direction, z toward the
            // viewer) and the cosine of its angular radius. One on the crown and three uneven rings round it, of
            // uneven sizes; the outermost near the rim, where the dome's curve narrows them to slivers.
            static const float4 Spots[SPOT_COUNT] =
            {
                float4( 0.054,  0.045, 0.998, 0.9840),
                float4( 0.348,  0.093, 0.933, 0.9915),
                float4( 0.013,  0.360, 0.933, 0.9880),
                float4(-0.348,  0.093, 0.933, 0.9930),
                float4(-0.222, -0.284, 0.933, 0.9895),
                float4( 0.206, -0.295, 0.933, 0.9925),
                float4( 0.422,  0.468, 0.777, 0.9875),
                float4(-0.236,  0.584, 0.777, 0.9935),
                float4(-0.620,  0.109, 0.777, 0.9890),
                float4(-0.422, -0.468, 0.777, 0.9940),
                float4( 0.131, -0.616, 0.777, 0.9870),
                float4( 0.606, -0.174, 0.777, 0.9920),
                float4( 0.852,  0.120, 0.510, 0.9900),
                float4( 0.294,  0.808, 0.510, 0.9945),
                float4(-0.564,  0.649, 0.510, 0.9885),
                float4(-0.827, -0.237, 0.510, 0.9930),
                float4(-0.223, -0.831, 0.510, 0.9950),
                float4( 0.639, -0.575, 0.510, 0.9895)
            };

            // Each wart's edge ripples by two waves across the cap, sin(a + 2.1 i) sin(b + 1.3 i) for wart i. The
            // two angles are the same for every wart of a pixel, so their sines and cosines are taken once and
            // each wart's phase is added by sin(a + p) = sin a cos p + cos a sin p from this table: xy the cosine
            // and sine of 2.1 i, zw of 1.3 i. The same number as the sines it replaces, 36 of them a cap pixel.
            static const float4 SpotRipple[SPOT_COUNT] =
            {
                float4( 1.0000000,  0.0000000,  1.0000000,  0.0000000),
                float4(-0.5048461,  0.8632094,  0.2674988,  0.9635582),
                float4(-0.4902608, -0.8715758, -0.8568888,  0.5155014),
                float4( 0.9998586,  0.0168139, -0.7259323, -0.6877662),
                float4(-0.5192887,  0.8545989,  0.4685167, -0.8834547),
                float4(-0.4755369, -0.8796958,  0.9765876,  0.2151200),
                float4( 0.9994346,  0.0336230,  0.0539554,  0.9985433),
                float4(-0.5335844,  0.8457468, -0.9477216,  0.3190984),
                float4(-0.4606786, -0.8875670, -0.5609843, -0.8278265),
                float4( 0.9987280,  0.0504227,  0.6475963, -0.7619836),
                float4(-0.5477293,  0.8366556,  0.9074468,  0.4201670),
                float4(-0.4456900, -0.8951874, -0.1621144,  0.9867720),
                float4( 0.9977390,  0.0672081, -0.9941776,  0.1077537),
                float4(-0.5617193,  0.8273279, -0.3697683, -0.9291240),
                float4(-0.4305754, -0.9025546,  0.7963525, -0.6048328),
                float4( 0.9964679,  0.0839745,  0.7958150,  0.6055399),
                float4(-0.5755505,  0.8177663, -0.3705933,  0.9287952),
                float4(-0.4153391, -0.9096667, -0.9940815, -0.1086366)
            };

            // How soft a wart's edge is drawn goes by its radius on the dome, the sine of its angular radius. That
            // is sqrt(e (2 - e)) for e = 1 - the cosine, and e is under a twentieth for any wart at any size or
            // raggedness the material allows, so it is sqrt(2 e) to within a hundredth: this, sqrt(2 (1 - w)) for
            // each wart of Spots, times the spots' size and half the ripple. It spares a square root a wart a
            // pixel and changes only the width of the edge's anti-aliasing, by a few hundredths.
            static const float SpotEdge[SPOT_COUNT] =
            {
                0.1788854, 0.1303840, 0.1549193, 0.1183216, 0.1449138, 0.1224745,
                0.1581139, 0.1140175, 0.1483240, 0.1095445, 0.1612452, 0.1264911,
                0.1414214, 0.1048809, 0.1516575, 0.1183216, 0.1000000, 0.1449138
            };

            // How much of a shape covers the pixel, from its distance and the size of a pixel. No screen-space
            // derivative is taken of the distance itself: the shapes meet at hard seams, where one would be wrong.
            inline float Cover(float distance, float pixel)
            {
                return saturate(0.5 - distance / pixel);
            }

            // Near enough the distance to an ellipse's edge: how far outside it the point is, over how fast that
            // grows there.
            inline float EllipseDistance(float2 q, float2 radii)
            {
                float2 scaled = q / radii;
                float  k = max(length(scaled), 1e-4);
                return (k - 1.0) * k / max(length(scaled / radii), 1e-4);
            }

            // The cap at a point: a disc about its middle, narrowed along the lean by how far it has tipped. Answers
            // the point's distance from the cap's edge (negative inside), and where on the disc it is (1 at the
            // rim, along the lean and across it).
            float CapAt(float2 p, float4 cap, float3 tilt, out float2 disc)
            {
                float2 c = p - cap.xy;
                float2 q = float2(dot(c, cap.zw), dot(c, float2(-cap.w, cap.z)));
                float2 radii = max(float2(tilt.z * tilt.x, tilt.z), 1e-3);
                disc = q / radii;
                return EllipseDistance(q, radii);
            }

            // The stem at a point: its foot stays at the centre and it runs toward the cap's middle, slimmer as
            // it goes; with the cap cut away (bare 1) it is the stump. Answers the point's distance from its edge,
            // and how far across it the point is, from its axis (a length of 1 at the edge).
            float StemAt(float2 p, float2 capMiddle, float bare, out float2 across)
            {
                float2 tip   = capMiddle * STEM_REACH;
                float  along = saturate(dot(p, tip) / max(dot(tip, tip), 1e-4));
                float2 off   = p - tip * along;
                float  width = lerp(lerp(STEM_FOOT_RADIUS, STEM_TOP_RADIUS, along), STUMP_RADIUS, bare);
                across = off / width;
                return length(off) - width;
            }

            // One of a cut cap's two pieces at a point (side +1 the piece left of the lance's way, -1 right). The
            // piece's flight is undone (off the cut line, along the lance's way, turned about a point of its own,
            // shrunk toward its far rim) to find where on the whole cap the point was. Answers its distance from the piece's
            // edge as drawn, the cap's rim or the cut, negative inside; and where on the cap's disc it is and how
            // far in from the cut. move is xy how far off the line and along the way, zw cos and sin of the turn.
            float PieceAt(
                float2 p, float side, float4 cap, float3 tilt, float4 cutLine, float4 move, float size,
                out float2 disc, out float fromCut)
            {
                float2 way    = cutLine.zw;
                float2 beside = float2(-way.y, way.x);
                float2 onLine = cutLine.xy + way * dot(cap.xy - cutLine.xy, way);
                float2 pivot  = onLine + beside * (side * PIECE_PIVOT);
                float2 rim    = cap.xy + beside * (side * tilt.z);
                float2 r      = p - pivot - beside * (side * move.x) - way * move.y;
                float  turned = move.w * side;
                float2 whole  = pivot + float2(r.x * move.z + r.y * turned, r.y * move.z - r.x * turned);
                float2 was    = rim + (whole - rim) / size;
                fromCut = side * dot(was - cutLine.xy, beside);
                return max(CapAt(was, cap, tilt, disc), -fromCut) * size;
            }

            // Where a cap's middle is and which way and how far it has tipped, for a lean in radii.
            void CapFrame(float2 lean, float capRadius, out float4 cap, out float3 tilt)
            {
                float  far = length(lean);
                // At rest the cap leans no way; any direction does.
                float2 way = far > 1e-4 ? lean / far : float2(1.0, 0.0);
                // Eased toward its most, not cut off at it, so the cap's wobble has no corner in it.
                float  angle = CAP_MAX_TILT * tanh(far * _TiltPerLean / CAP_MAX_TILT);
                cap  = float4(lean, way);
                tilt = float3(cos(angle), sin(angle), capRadius);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                float radius = max(_Radius, 1e-4);
                float2 lean  = _Lean.xy / radius;
                float  capRadius = CAP_RADIUS * (1.0 + _Pulse * _PulseSwell) * _CapGrow;
                CapFrame(lean, capRadius, OUT.cap, OUT.tilt);
                float2 cutLean = _CutLean.xy / radius;
                // The cap that was cut, as it stood: where it leaned to, and its size (z: under 1 only for one cut
                // as it popped back).
                CapFrame(cutLean, CAP_RADIUS * _CutLean.z, OUT.cutCap, OUT.cutTilt);

                float turn = _Seed * BP_TAU;

                float2 worldCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                OUT.toLight = normalize(float3(SceneLightDirectionAtLOD(worldCenter), _LightHeight));
                // The game's own measure of night: how dark the time of day's light is (as SceneLightDesaturate
                // reads it), with no light of the board's in it, so a blast beside a mushroom does not put its
                // glow out. A uniform's worth of work here, and nothing more a pixel.
                float dark = 1.0 - saturate(dot(SceneLightTint(), float3(0.2126, 0.7152, 0.0722)));
                OUT.halfway = float4(normalize(OUT.toLight + float3(0, 0, 1)),
                                     _NightGlow * smoothstep(_NightGlowFrom, _NightGlowFull, dark));
                float3 tint = SceneLightTintAtLOD(worldCenter);
                OUT.groundTint = tint;
                OUT.lightTint  = tint + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);

                // The flow is not normalised: where a local light cancels the day's, the shadow shrinks under the
                // mushroom and grows out the other side instead of jumping across (SpriteLightDriven's orbit).
                float2 downLight = -SceneLightFlowAtLOD(worldCenter, _ShadowReceiverRadius);
                OUT.shadow = float3(downLight * (_ShadowDistance / radius),
                                    _ShadowColor.a * ShadowLightFadeAtLOD(worldCenter));

                OUT.color = half4(_Color.rgb, _Color.a * GroundSceneryAt(worldCenter));

                // The mesh is the most room the mushroom can ever need (the builder sizes it for the largest lean,
                // swell, shadow and cut at once, each way). Most frames it needs a fraction of that, and every pixel
                // of the quad pays for both shapes, so each corner is pulled in to a box about what is drawn now:
                // the cap where the lean has carried it and the dark under it, a cut cap's pieces as far as they
                // have flown, the same box again where the shadow lies, and the edges' fade. Never past the mesh's
                // own corner, so the renderer's bounds still hold it.
                // With no cap there is no dark under one either, and the box is the stem's and its roots'.
                float  foot     = capRadius > 0.0 ? FOOT_SHADE_RADIUS : ROOT_REACH;
                float2 bodyLow  = min(float2(-foot, -foot), lean - capRadius);
                float2 bodyHigh = max(float2(foot, foot), lean + capRadius);
                if (_CutFall.x > 0.0)
                {
                    float flown = CAP_RADIUS * (max(_CutPieces.w, 1.0) + abs(_CutPieces.z))
                                + (abs(_CutPieces.x) + abs(_CutPieces.y)) / radius;
                    bodyLow  = min(bodyLow, cutLean - flown);
                    bodyHigh = max(bodyHigh, cutLean + flown);
                }

                // A cap a blast has lifted throws its shadow further off than one on its stem does.
                float2 cast     = OUT.shadow.xy * (_CutFall.x > 0.0 ? clamp(_CutFall.y, 1.0, PIECE_SHADOW_MOST) : 1.0);
                float  fade     = _ShadowSoftness / radius * QUAD_SHADOW_ROOM + QUAD_EDGE_ROOM;
                float2 low      = min(bodyLow - QUAD_EDGE_ROOM, bodyLow + cast - fade);
                float2 high     = max(bodyHigh + QUAD_EDGE_ROOM, bodyHigh + cast + fade);
                float2 room     = abs(IN.vertex.xy) / radius;
                float2 corner   = float2(IN.vertex.x < 0.0 ? max(low.x, -room.x) : min(high.x, room.x),
                                         IN.vertex.y < 0.0 ? max(low.y, -room.y) : min(high.y, room.y));

                // Object space, so the mushroom moves and scales with its transform (the scenario root's ascent
                // and its mist reveal).
                OUT.position = float4(corner, cos(turn), sin(turn));
                OUT.vertex   = UnityObjectToClipPos(float4(corner * radius, IN.vertex.z, 1.0));
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float2 p = IN.position.xy;

                // A pixel's size in radii. Everything below is anti-aliased from it, and it is the one derivative
                // taken: before anything branches, since one inside a branch is undefined on the device's GLES.
                float pixel = max(length(fwidth(p)) * BP_HALF_SQRT2, 1e-4);

                // How much of the whole cap there is: all of it but while it is cut away or growing back.
                float capThere = step(0.02, IN.tilt.z);
                float bare     = 1.0 - saturate(IN.tilt.z);

                float2 disc;
                float2 stemAcross;
                float2 unused;
                float  capEdge  = CapAt(p, IN.cap, IN.tilt, disc);
                float  stemEdge = StemAt(p, IN.cap.xy, bare, stemAcross);
                float  capCast  = lerp(1e3, CapAt(p - IN.shadow.xy, IN.cap, IN.tilt, unused), capThere);
                float  stemCast = StemAt(p - IN.shadow.xy * lerp(1.0, STUMP_SHADOW, bare), IN.cap.xy, bare, unused);

                // About the foot, by angle and distance: the roots on the ground, and the stalk's torn edge. Only
                // within the roots' reach and where the whole cap is not over the pixel, so under a cap at rest no
                // pixel pays for it. The angle is turned by the seed and every ripple shifted by it, so no two
                // mushrooms root or tear alike.
                float rootLine  = 0.0;
                float rootHalo  = 0.0;
                float footAngle = 0.0;
                float seedPhase = _Seed * 61.0;
                UNITY_BRANCH
                if (dot(p, p) < ROOT_REACH * ROOT_REACH && (capThere < 0.5 || capEdge > -pixel))
                {
                    float2 q   = float2(p.x * IN.position.z + p.y * IN.position.w,
                                        p.y * IN.position.z - p.x * IN.position.w);
                    float  far = max(length(q), 1e-4);
                    footAngle  = atan2(q.y, q.x);

                    // The roots stand unevenly round the foot and bend more the further out they run.
                    // The warp's slope by the angle stays over zero, or a root would fold back and smear.
                    float around = footAngle + 0.15 * sin(footAngle * 2.0 + seedPhase)
                                 + 0.05 * sin(footAngle * 5.0 - seedPhase * 1.3)
                                 + _RootBend * far * sin(far * 5.0 + seedPhase * 0.5 + 1.2 * sin(footAngle + seedPhase));
                    float sector = BP_TAU / ROOT_COUNT;
                    float nearest = floor(around / sector + 0.5);
                    // The same root either side of the angle's seam.
                    float which  = nearest - ROOT_COUNT * floor(nearest / ROOT_COUNT);
                    float along  = far / (ROOT_REACH * (0.86 + 0.14 * sin(which * 2.4 + seedPhase)));
                    // A fork is the angle folded about a line that parts from its parent's, so the two branches
                    // start where the parent is and no branch begins from nothing.
                    float off = abs(around - nearest * sector);
                    off = abs(off - _RootFork * sector * saturate((along - ROOT_FORK_AT) / (1.0 - ROOT_FORK_AT)));
                    off = abs(off - 0.45 * _RootFork * sector
                                  * saturate((along - ROOT_FORK_AGAIN) / (1.0 - ROOT_FORK_AGAIN)));
                    // Thinner out along its length and at each fork, to nothing at the tip; a root thinner than
                    // a pixel fades instead of breaking up.
                    float width = _RootWidth * saturate(1.0 - along * along)
                                * lerp(1.0, 0.7, step(ROOT_FORK_AT, along)) * lerp(1.0, 0.75, step(ROOT_FORK_AGAIN, along));
                    rootLine = Cover(off * far - width, pixel) * saturate(width / pixel);
                    rootHalo = smoothstep(width * 2.2 + 2.0 * pixel, width * 0.6, off * far) * saturate(width / pixel);

                    stemEdge += bare * STUMP_TEAR
                              * (0.5 + 0.3 * sin(footAngle * 5.0 + 2.0 * sin(footAngle * 2.0 + seedPhase)
                                                 + seedPhase * 1.7)
                                     + 0.2 * sin(footAngle * 11.0 - seedPhase * 0.9));
                }

                // A cut cap's two pieces, and their shadows, which close in under them as they fall. Skipped whole
                // on a uniform while no cap is cut. A pixel is on one piece or the other, so the cap is still
                // shaded once below, wherever the piece's pixel was on the whole cap. A cap a blast blew off is
                // one piece, the whole cap (the line it is cut by lies outside it): _CutFall.z says so, and the
                // piece that would be empty is not worked out.
                float  pieceShows = 0.0;
                float  pieceCover = 0.0;
                float  pieceShade = 0.0;
                float  pieceSize  = 1.0;
                float  fromCut    = 1.0;
                float2 pieceDisc  = float2(0.0, 0.0);
                UNITY_BRANCH
                if (_CutFall.x > 0.0)
                {
                    float  radius  = max(_Radius, 1e-4);
                    float4 cutLine = float4(_CutLine.xy / radius, _CutLine.zw);
                    float4 move    = float4(_CutPieces.xy / radius, cos(_CutPieces.z), sin(_CutPieces.z));
                    pieceSize = max(_CutPieces.w, 0.05);

                    float2 discLeft;
                    float2 discRight;
                    float  cutLeft;
                    float  cutRight;
                    float2 under = p - IN.shadow.xy * clamp(_CutFall.y, 0.0, PIECE_SHADOW_MOST);
                    float  left  = Cover(PieceAt(p, 1.0, IN.cutCap, IN.cutTilt, cutLine, move, pieceSize,
                                                 discLeft, cutLeft), pixel);
                    float  pieceCast = PieceAt(under, 1.0, IN.cutCap, IN.cutTilt, cutLine, move, pieceSize,
                                               unused, cutRight);
                    float  right = 0.0;
                    UNITY_BRANCH
                    if (_CutFall.z < 0.5)
                    {
                        float castRight;
                        right = Cover(PieceAt(p, -1.0, IN.cutCap, IN.cutTilt, cutLine, move, pieceSize,
                                              discRight, cutRight), pixel);
                        pieceCast = min(pieceCast, PieceAt(under, -1.0, IN.cutCap, IN.cutTilt, cutLine, move,
                                                           pieceSize, unused, castRight));
                    }
                    else
                    {
                        discRight = discLeft;
                        cutRight  = cutLeft;
                    }

                    pieceCover = max(left, right);
                    pieceShows = pieceCover * saturate(_CutFall.x);
                    pieceDisc  = left >= right ? discLeft : discRight;
                    fromCut    = left >= right ? cutLeft : cutRight;
                    float  pieceSoft = max(_ShadowSoftness / radius, pixel);
                    pieceShade = smoothstep(pieceSoft, -pieceSoft, pieceCast) * IN.shadow.z * saturate(_CutFall.x);
                }

                // On the ground: the cast shadow, the whole shape again down-light, the pieces' shadows, and the
                // dark under the cap's overhang, which stays about the foot when the cap is knocked aside and goes
                // with the cap when it is cut away.
                float softness   = max(_ShadowSoftness / max(_Radius, 1e-4), pixel);
                float castShade  = smoothstep(softness, -softness, min(capCast, stemCast)) * IN.shadow.z;
                float footShade  = saturate(1.0 - dot(p, p) / (FOOT_SHADE_RADIUS * FOOT_SHADE_RADIUS));
                footShade *= footShade * _FootShadow * (1.0 - bare);
                float shadeAlpha = 1.0 - (1.0 - castShade) * (1.0 - pieceShade) * (1.0 - footShade);

                // The cap, whole or in pieces, lies over the stem. Summed by coverage, so the cap's soft edge shows
                // the stem behind it. A piece hides the stem and the roots for as long as it is over them, however
                // far it has faded: they come out from under its edge as it draws back, not through it.
                float onCap     = max(Cover(capEdge, pixel) * capThere, pieceShows);
                float onPiece   = step(Cover(capEdge, pixel) * capThere, pieceShows) * step(1e-4, pieceShows);
                float hidden    = max(Cover(capEdge, pixel) * capThere, pieceCover);
                float onStem    = Cover(stemEdge, pixel);
                float stemShare = onStem * (1.0 - hidden);
                float bodyAlpha = onCap + stemShare;

                // The roots lie on the ground: over the shade there, under everything that stands.
                float uncovered   = (1.0 - hidden) * (1.0 - onStem);
                float rootAlpha   = rootLine * uncovered * _RootColor.a;
                shadeAlpha = 1.0 - (1.0 - shadeAlpha) * (1.0 - rootHalo * uncovered * _RootShade * IN.shadow.z);
                float groundAlpha = rootAlpha + shadeAlpha * (1.0 - rootAlpha);

                float alpha = bodyAlpha + groundAlpha * (1.0 - bodyAlpha);
                if (alpha < 0.004)
                {
                    discard;
                }

                // Each part is shaded only where it shows: the spots and the sheen are most of this shader's
                // cost, and a part's colour counts for nothing where its share is none.
                half3  cap    = half3(0.0, 0.0, 0.0);
                float  spot   = 0.0;
                UNITY_BRANCH
                if (onCap > 0.0)
                {
                    // The cap this pixel is on: the whole one, or the one that was cut, where the piece's pixel
                    // was on it.
                    float2 capDisc  = lerp(disc, pieceDisc, onPiece);
                    float2 way      = lerp(IN.cap.zw, IN.cutCap.zw, onPiece);
                    float2 tipCos   = lerp(IN.tilt.xy, IN.cutTilt.xy, onPiece);
                    float  capPixel = pixel / lerp(1.0, pieceSize, onPiece);
                    float2 across   = float2(-way.y, way.x);

                    // The dome, in the cap's own frame: x along the lean, y across it, z up out of the cap.
                    float  discR = min(length(capDisc), 0.999);
                    float2 onDisc = capDisc * (discR / max(length(capDisc), 1e-4));
                    float2 onDome = onDisc * _DomeCurve;
                    float3 own = float3(onDome, sqrt(1.0 - dot(onDome, onDome)));

                    // Tipped into the lean, its crown turns that way, and the light it catches with it.
                    float3 tipped = float3(own.x * tipCos.x + own.z * tipCos.y, own.y,
                                           own.z * tipCos.x - own.x * tipCos.y);
                    float3 capNormal = float3(way * tipped.x + across * tipped.y, tipped.z);
                    // Less wrapped than the stem's light: the far side of the dome has to fall into shade, or from
                    // above the cap reads as a flat disc.
                    float  capFacing = saturate(dot(capNormal, IN.toLight) * 0.75 + 0.3);
                    cap = lerp(_CapShade.rgb, _CapLit.rgb, capFacing);

                    // The spots lie on the dome and tip with it, so they are found in the cap's own frame, turned
                    // back from the lean's and about the crown by the seed. They narrow toward the rim.
                    float2 level  = (way * onDisc.x + across * onDisc.y) * SPOT_CURVE;
                    float  crown  = sqrt(1.0 - dot(level, level));
                    float3 turned = float3(level.x * IN.position.z + level.y * IN.position.w,
                                           level.y * IN.position.z - level.x * IN.position.w, crown);
                    float  onSphere = capPixel / max(crown * max(tipCos.x, 0.3), 0.25);
                    // A wart is a ragged patch, not a disc: its edge is pushed in and out by a ripple of its own
                    // (SpotRipple has each wart's phase; the two waves are taken here, once a pixel).
                    float2 waveA;
                    float2 waveB;
                    sincos(dot(turned.xy, float2(31.0, 17.0)), waveA.x, waveA.y);
                    sincos(dot(turned.xy, float2(-13.0, 29.0)), waveB.x, waveB.y);
                    float  softest = onSphere * _SpotSize;
                    for (int i = 0; i < SPOT_COUNT; i++)
                    {
                        float4 phase = SpotRipple[i];
                        float lump  = (waveA.x * phase.x + waveA.y * phase.y) * (waveB.x * phase.z + waveB.y * phase.w);
                        float reach = 1.0 - (1.0 - Spots[i].w) * _SpotSize * _SpotSize * (1.0 + _SpotLumps * lump);
                        float soft  = softest * SpotEdge[i] * (1.0 + 0.5 * _SpotLumps * lump);
                        spot = max(spot, Cover(reach - dot(turned, Spots[i].xyz), soft));
                    }

                    // The cap's zones, from the middle out: a darker crown, the cap's own colour, and a paler
                    // margin pleated as a toadstool's is over its gills. The margin's inner edge follows the
                    // pleats. The pleats fade where one is thinner than a pixel.
                    float pleat = cos(atan2(level.y, level.x) * _PleatCount);
                    float pleatWidth = _PleatCount * capPixel / max(discR * max(tipCos.x, 0.3), 0.05);
                    pleat *= saturate(1.0 - pleatWidth / UNITY_PI);
                    float margin = smoothstep(_MarginStart, _MarginStart + 0.14, discR + 0.035 * pleat);
                    float shade  = lerp(0.6, 1.0, capFacing);
                    cap = lerp(cap, _CrownColor.rgb * shade, smoothstep(_CrownSize, 0.0, discR) * _CrownColor.a);
                    cap = lerp(cap, _MarginColor.rgb * shade, margin * _MarginColor.a);
                    cap = lerp(cap, _CapEdge.rgb, margin * saturate(0.5 - 0.5 * pleat) * _Pleats);
                    cap = lerp(cap, _SpotColor.rgb * lerp(0.7, 1.0, capFacing), spot);
                    cap = lerp(cap, _CapEdge.rgb, pow(discR, _EdgePower) * (1.0 - 0.5 * spot));
                    cap += pow(saturate(dot(capNormal, IN.halfway.xyz)), _SheenPower) * _SheenIntensity;

                    // A piece shows the cap's pale flesh along its cut edge, which is what makes the slice read.
                    UNITY_BRANCH
                    if (_CutFall.x > 0.0)
                    {
                        float flesh = onPiece * (1.0 - smoothstep(_FleshBand * 0.6, _FleshBand, fromCut));
                        cap = lerp(cap, _FleshColor.rgb * lerp(0.82, 1.0, capFacing), flesh);
                    }
                }

                // The stem: round as a column, in the cap's shade where it runs in under it. With the cap cut away
                // it is seen from above: a flat pale top, a darker rim where the cap sat, and its flank round that.
                half3 stem = half3(0.0, 0.0, 0.0);
                UNITY_BRANCH
                if (stemShare > 0.0)
                {
                    float2 capWay = IN.cap.zw;
                    float  side = clamp(dot(stemAcross, float2(-capWay.y, capWay.x)), -0.999, 0.999);
                    float3 stemNormal = float3(float2(-capWay.y, capWay.x) * side, sqrt(1.0 - side * side));
                    float  stemFacing = saturate(dot(stemNormal, IN.toLight) * 0.5 + 0.5);
                    stem = lerp(_StemShade.rgb, _StemLit.rgb, stemFacing);
                    // Darkest right under the cap's edge.
                    stem *= 1.0 - _CapOcclusion * smoothstep(0.45, 0.0, capEdge) * capThere;
                    stem = lerp(stem, _StemShade.rgb * 0.8, pow(abs(side), 4.0) * 0.6);

                    UNITY_BRANCH
                    if (bare > 0.0)
                    {
                        // The cut stalk from above: fibres running out from a darker, hollow middle to an uneven
                        // line where the cap tore away, and round that a skirt of what is left of the gills.
                        float  out01   = min(length(stemAcross), 0.999);
                        float2 outward = stemAcross / max(out01, 1e-3);
                        float  torn    = STUMP_TOP_RADIUS / STUMP_RADIUS
                                       * (1.0 + 0.1 * sin(footAngle * 4.0 + seedPhase * 1.1)
                                              + 0.07 * sin(footAngle * 9.0 - seedPhase * 0.6));
                        float  skirt   = smoothstep(torn - 0.06, torn + 0.06, out01);
                        float  hollow  = smoothstep(torn * 0.55, 0.0, out01);

                        // The top dips to its middle and the skirt falls away outward: both catch the light.
                        float  slope  = lerp(-1.6 * hollow * (1.0 - hollow), 0.7, skirt);
                        float3 normal = float3(outward * slope, sqrt(1.0 - slope * slope));
                        float  facing = saturate(dot(normal, IN.toLight) * 0.5 + 0.5);

                        // Fibres and gills are spaced unevenly, and fade where one is thinner than a pixel.
                        float  ring   = max(out01 * STUMP_RADIUS, 0.02);
                        float  fibre  = sin(footAngle * STUMP_FIBRES + 2.5 * sin(footAngle * 3.0 + seedPhase)
                                            + out01 * 2.0)
                                      * saturate(1.0 - STUMP_FIBRES * pixel / ring / UNITY_PI);
                        float  gill   = sin(footAngle * STUMP_GILLS + 1.5 * sin(footAngle * 4.0 - seedPhase))
                                      * saturate(1.0 - STUMP_GILLS * pixel / ring / UNITY_PI);

                        half3 top = lerp(_StumpShade.rgb, _StumpColor.rgb, facing) * (1.0 - _StumpGrain * fibre);
                        top = lerp(top, _StumpShade.rgb * 0.7, hollow * 0.75);
                        half3 remnant = lerp(_CapEdge.rgb, _MarginColor.rgb * 0.7, 0.5 + 0.5 * gill)
                                      * lerp(0.55, 1.0, facing) * (1.0 - 0.45 * smoothstep(torn, 1.0, out01));
                        half3 stump = lerp(top, remnant, skirt);
                        // The tear itself, with the cap's own shade in it.
                        stump = lerp(stump, _CapShade.rgb * 0.5, 0.55 * (1.0 - smoothstep(0.0, 0.1, abs(out01 - torn))));
                        stem = lerp(stem, stump, bare);
                    }
                }

                half3 body = (cap * onCap + stem * stemShare) / max(bodyAlpha, 1e-4);

                // The body takes the balloons' re-graded light; nothing on the board blooms but effects
                // (Plans/PLAN-HDR). The spots' flare when a chain pays is one, so it goes on after the cap.
                half3 tint = lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence) * IN.color.rgb;
                // So is the warts' glow at night, the owner's one exception to that rule: on a whole cap and on
                // its pieces in the air, never on the stem or the roots.
                float warts = spot * onCap / max(bodyAlpha, 1e-4);
                body = SceneLightCapToWhite(body * tint)
                     + _SpotColor.rgb * (warts * saturate(_Pulse) * _PulseGlow)
                     + _NightGlowTint.rgb * (warts * IN.halfway.w);

                // On the ground the roots lie over the shade, and in it where the cap or a piece stands over them.
                half3 ground = (_RootColor.rgb * (rootAlpha * (1.0 - 0.35 * shadeAlpha))
                                + _ShadowColor.rgb * (shadeAlpha * (1.0 - rootAlpha))) * IN.groundTint;
                half3 col = body * bodyAlpha + ground * (1.0 - bodyAlpha);
                return half4(col, alpha) * IN.color.a;
            }
            ENDCG
        }
    }
}
