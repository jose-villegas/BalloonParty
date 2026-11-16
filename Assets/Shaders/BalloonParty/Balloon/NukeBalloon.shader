Shader "BalloonParty/Balloon/NukeBalloon"
{
    // The nuke reward balloon's body (PLAN-NukeBalloon): the golden balloon's plated sphere in dull lead, its
    // hexagonal plates bolted at the corners over a dark rubber skin, with a round danger placard riveted to the
    // face: a black radiation trefoil on yellow inside a black ring. The plates take the scene light as the golden
    // ones do, but rough: a broad weak sheen, no glitter. Each ordinary hit the nuke takes dents more of its plates
    // (_DamageProgress, pushed by the prefab's ToughBalloonVariant as on the tough balloon): a dented plate sinks
    // darker and tips the other way. Below the placard sits an LED counter, one seven-segment digit behind dark
    // glass, and above it a domed lamp; NukeFuseView pushes the fuse they show (_FuseArmed, _FuseDigit, _FuseBeat).
    // Unarmed, the LED shows a green heart and the lamp pulses green; armed, the LED counts the fuse down in bright
    // red digits and the lamp turns red, flashing with each count. Both shine by themselves, so the night doesn't dim
    // them. A thin line of light runs along the seams between the plates: HDR green while it waits, dim red once
    // armed, flaring bright red on each count. The seam light, the LED's heart and digits and the lamp are the parts
    // of a balloon that bloom (Plans/PLAN-HDR caps the rest at white), so they are added after the cap. The shadow
    // comes from the prefab's baked shadow child.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Lead)]
        _GoldLit    ("Lit",    Color) = (1.00, 0.87, 0.32, 1)
        _GoldShade  ("Shade",  Color) = (0.58, 0.36, 0.03, 1)
        _GoldEdge   ("Edge",   Color) = (0.26, 0.14, 0.01, 1)
        _EdgePower  ("Edge Falloff", Range(1, 8)) = 3.0
        // A metal ball's reflected horizon: bright sky above it, dark ground below, broken up by
        // the plates' tilts. Fixed to the balloon's up, whatever the light does.
        _EnvStrength ("Horizon Strength", Range(0, 1)) = 0.4
        _EnvHorizon  ("Horizon Height", Range(-0.8, 0.8)) = -0.05
        // The light's height over the board: low rakes across the ball, high lights it face-on.
        _LightHeight ("Light Height", Range(0.1, 3)) = 0.9

        [Header(Plates)]
        _PlateScale       ("Plates Across the Radius", Range(1, 8)) = 2.3
        // How far in from a seam each plate slopes down to it, as a share of the plate's inner
        // radius (0.5), and how steeply: the relief.
        _BevelWidth       ("Bevel Width", Range(0, 0.45)) = 0.16
        _BevelDepth       ("Bevel Depth", Range(0, 2)) = 0.7
        _PlateTilt       ("Tilt", Range(0, 0.6)) = 0.16
        _PlateVariance    ("Brightness Variance", Range(0, 0.5)) = 0.12
        _PlateWobble      ("Wobble", Range(0, 1)) = 0.35
        _PlateWobbleSpeed ("Wobble Speed", Range(0, 2)) = 0.4

        [Header(Bolts)]
        // Two bolts per plate, inset from its top and bottom corners, in plate units (the plate's
        // inner radius is 0.5).
        _BoltRadius ("Size", Range(0.01, 0.15)) = 0.075
        _BoltInset  ("Inset From the Corner", Range(0.3, 0.95)) = 0.62
        _BoltDome   ("Dome", Range(0, 3)) = 1.4
        _BoltRingColor ("Ring", Color) = (0.36, 0.20, 0.03, 1)

        [Header(Thickness)]
        // How far the plate tops stand over the rubber, as a share of the balloon's radius.
        _PlateThickness ("Thickness", Range(0, 0.2)) = 0.07
        // How much darker a side wall gets from its top down to the rubber.
        _WallShade      ("Wall Darkening", Range(0, 1)) = 0.5
        // The shadow each plate casts on the rubber, away from the light.
        _CastShadow     ("Cast Shadow", Range(0, 1)) = 0.55

        [Header(Balloon Between the Plates)]
        // Half the gap between two plates, in plate units.
        _GapWidth         ("Gap", Range(0, 0.2)) = 0.11
        _RubberLit        ("Lit", Color) = (1.00, 0.66, 0.25, 1)
        _RubberShade      ("Shade", Color) = (0.55, 0.22, 0.05, 1)
        _RubberGloss      ("Gloss", Range(0, 2)) = 1.2
        _RubberGlossPower ("Gloss Sharpness", Range(4, 128)) = 24
        // The rubber darkening at each plate's foot.
        _GapShadow        ("Contact Shadow", Range(0, 1)) = 0.3

        [Header(Glint)]
        _GlintColor     ("Color", Color) = (1.00, 0.96, 0.80, 1)
        _GlintPower     ("Sharpness", Range(4, 200)) = 24
        _GlintIntensity ("Intensity", Range(0, 4)) = 2.4
        // The broad metallic sheen under the glint, per plate too.
        _SheenPower     ("Sheen Sharpness", Range(1, 32)) = 5
        _SheenIntensity ("Sheen Intensity", Range(0, 2)) = 0.5

        [Header(Danger Placard)]
        // In units of the balloon's radius, from the face's centre; lifted a little above the middle.
        _MarkRadius ("Radius", Range(0.1, 0.8)) = 0.36
        _MarkLift   ("Lift", Range(-0.3, 0.3)) = 0.08
        // Where the black ring starts, as a share of the placard's radius.
        _MarkRing   ("Ring Start", Range(0.6, 0.98)) = 0.86
        _MarkYellow ("Yellow", Color) = (1.00, 0.76, 0.06, 1)
        _MarkBlack  ("Black", Color) = (0.06, 0.05, 0.04, 1)
        _MarkWear   ("Wear", Range(0, 0.5)) = 0.12

        [Header(Fuse)]
        // Half the LED housing's width and height, and its gap under the placard, in units of the balloon's radius.
        _LedSize    ("LED Half Size", Vector) = (0.15, 0.12, 0, 0)
        _LedGap     ("LED Gap", Range(0, 0.2)) = 0.05
        _LedColor   ("LED", Color) = (1.00, 0.12, 0.06, 1)
        _LedGlass   ("LED Glass", Color) = (0.05, 0.04, 0.04, 1)
        // How bright the unarmed heart shows, as a share of the armed digits.
        _LedIdle    ("LED Idle", Range(0, 1)) = 0.6
        _LampRadius ("Lamp Radius", Range(0.02, 0.2)) = 0.075
        _LampGap    ("Lamp Gap", Range(0, 0.2)) = 0.04
        _LampGreen  ("Lamp Waiting", Color) = (0.25, 1.00, 0.35, 1)
        _LampRed    ("Lamp Armed", Color) = (1.00, 0.10, 0.05, 1)
        // The glow the lamp throws on the plating around it, as a share of the lamp's own light.
        _LampHalo   ("Lamp Halo", Range(0, 1)) = 0.35
        // How far past white the LED's heart and digits and the lamp shine, so they bloom.
        _FuseGlow   ("Glow (HDR)", Range(1, 8)) = 3
        [Header(Seam Light)]
        // A line of light along the seams between the plates, in plate units, and the glow it spills over the
        // rubber either side.
        _SeamWidth     ("Line Half Width", Range(0, 0.1)) = 0.028
        _SeamHalo      ("Halo", Range(0, 1)) = 0.4
        [HDR] _SeamGreen ("Waiting", Color) = (0.5, 3.2, 0.8, 1)
        [HDR] _SeamRed   ("Armed Count", Color) = (9.0, 0.6, 0.25, 1)
        // How bright the red line stays between counts, as a share of its flare on a count.
        _SeamArmedIdle ("Armed Between Counts", Range(0, 1)) = 0.025
        [HideInInspector] _FuseArmed ("Armed", Float) = 0
        [HideInInspector] _FuseDigit ("Digit", Float) = 3
        [HideInInspector] _FuseBeat  ("Last Count", Float) = -100

        [Header(Dents)]
        // The share of the plates dented once the nuke is a hit from going off.
        _DentShare  ("Share at Full Damage", Range(0, 1)) = 0.55
        _DentDarken ("Darkening", Range(0, 1)) = 0.35
        [HideInInspector] _DamageProgress ("Damage Progress", Range(0, 1)) = 0
        [HideInInspector] _VoronoiSeed ("Seed", Vector) = (0, 0, 0, 0)

        [Header(Rim)]
        _RimColor ("Color", Color) = (0.55, 0.36, 0.08, 1)
        _RimWidth ("Width", Range(0, 0.5)) = 0.12
        _RimBlend ("Blend", Range(0, 1)) = 0.6

        [Header(Lighting)]
        _LightInfluence ("Light Influence", Range(0, 1)) = 1
        _AmbientHue     ("Ambient Hue", Range(0, 1)) = 0.25
        _AmbientDimming ("Ambient Dimming", Range(0, 1)) = 0.4

        _SpriteScale ("Sprite Scale", Range(0.1, 1.0)) = 1.0
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing

            // Depth steps through the plates' thickness: enough that a wall shows no banding at the rim.
            #define WALL_STEPS 6
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            // For Hash21; the glitter itself is left off the lead.
            #include "../Include/Glitter.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 worldPos  : TEXCOORD1;
                // xy = world-space object centre, z = world-space extent (X scale).
                float3 worldData : TEXCOORD2;
                // The light is sampled once, at the balloon's centre, so these are the same at every
                // vertex and need no renormalizing per pixel.
                float3 toLight   : TEXCOORD3;
                float3 halfway   : TEXCOORD4;
                float3 lightTint : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            sampler2D _MainTex;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;

            half4  _GoldLit;
            half4  _GoldShade;
            half4  _GoldEdge;
            float  _EdgePower;
            float  _EnvStrength;
            float  _EnvHorizon;
            float  _LightHeight;

            float  _PlateScale;
            float  _BevelWidth;
            float  _BevelDepth;
            float  _PlateTilt;
            float  _PlateVariance;
            float  _PlateWobble;
            float  _PlateWobbleSpeed;

            float  _BoltRadius;
            float  _BoltInset;
            float  _BoltDome;
            half4  _BoltRingColor;

            float  _GapWidth;
            half4  _RubberLit;
            half4  _RubberShade;
            float  _RubberGloss;
            float  _RubberGlossPower;
            float  _GapShadow;

            float  _PlateThickness;
            float  _WallShade;
            float  _CastShadow;

            half4  _GlintColor;
            float  _GlintPower;
            float  _GlintIntensity;
            float  _SheenPower;
            float  _SheenIntensity;

            float  _MarkRadius;
            float  _MarkLift;
            float  _MarkRing;
            half4  _MarkYellow;
            half4  _MarkBlack;
            float  _MarkWear;

            float4 _LedSize;
            float  _LedGap;
            half4  _LedColor;
            half4  _LedGlass;
            float  _LedIdle;
            float  _LampRadius;
            float  _LampGap;
            half4  _LampGreen;
            half4  _LampRed;
            float  _LampHalo;
            float  _FuseGlow;
            float  _SeamWidth;
            float  _SeamHalo;
            half4  _SeamGreen;
            half4  _SeamRed;
            float  _SeamArmedIdle;
            float  _FuseArmed;
            float  _FuseDigit;
            float  _FuseBeat;

            float  _DentShare;
            float  _DentDarken;
            float  _DamageProgress;
            float4 _VoronoiSeed;

            half4  _RimColor;
            float  _RimWidth;
            float  _RimBlend;

            float  _LightInfluence;
            float  _AmbientHue;
            float  _AmbientDimming;
            float  _SpriteScale;

            // Two hashes from one: half the hashing of two Hash21 calls.
            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            // The hexagonal plate under uv, on a lattice whose neighbouring centres are 1 apart and
            // with one centred on the origin, so the sphere's middle plate faces the camera square.
            // Returns the plate's centre; out, the distance to its nearest edge and that edge's
            // outward direction, which the bevel slopes along.
            float2 HexPlate(float2 uv, out float edgeDistance, out float2 edgeDirection)
            {
                const float2 lattice = float2(1.0, 1.7320508);
                float2 shifted = uv - 0.5 * lattice;
                float2 a = uv - lattice * floor(uv / lattice + 0.5);
                float2 b = shifted - lattice * floor(uv / lattice);
                float2 offset = dot(a, a) < dot(b, b) ? a : b;

                float2 side  = step(0.0, offset) * 2.0 - 1.0;
                float2 abs0  = abs(offset);
                float  slant = dot(abs0, float2(0.5, 0.8660254));
                edgeDistance  = 0.5 - max(abs0.x, slant);
                edgeDirection = abs0.x > slant ? float2(side.x, 0.0) : float2(0.5, 0.8660254) * side;
                return uv - offset;
            }

            // The danger placard riveted to the face: a radiation trefoil, black on yellow inside a black ring, its
            // three blades pointing down and up to either side. It is a flat steel disc on the sphere, so it takes
            // the sphere's light, a soft sheen and some wear, and three rivets hold it at its ring between the blades.
            half3 DangerPlacard(half3 col, float2 p, float3 normal, float3 toLight, float3 halfway)
            {
                float2 q = (p - float2(0.0, _MarkLift)) / _MarkRadius;
                float  r = length(q);
                float  rSoft = max(fwidth(r), 0.01);
                float  placard = 1.0 - smoothstep(1.0 - rSoft, 1.0 + rSoft, r);
                if (placard <= 0.0)
                {
                    return col;
                }

                const float third = UNITY_PI * 2.0 / 3.0;
                float  a = atan2(q.y, q.x);
                // Radians from the nearest blade's centre line; the blades are centred at 30, 150 and 270 degrees.
                float  fromBlade = abs(frac((a - UNITY_PI / 6.0) / third + 0.5) - 0.5) * third;
                float  insideBlade = (UNITY_PI / 6.0 - fromBlade) * r;
                float  aSoft = max(fwidth(insideBlade), 0.01);
                float  blade = smoothstep(-aSoft, aSoft, insideBlade)
                             * smoothstep(0.25 - rSoft, 0.25 + rSoft, r)
                             * (1.0 - smoothstep(0.76 - rSoft, 0.76 + rSoft, r));
                float  hub  = 1.0 - smoothstep(0.16 - rSoft, 0.16 + rSoft, r);
                float  ring = smoothstep(_MarkRing - rSoft, _MarkRing + rSoft, r);
                half3  mark = lerp(_MarkYellow.rgb, _MarkBlack.rgb, saturate(max(max(blade, hub), ring)));

                float  wear = 1.0 - _MarkWear * Hash21(floor(q * 9.0) + 40.0);
                float  lit  = lerp(0.55, 1.15, saturate(dot(normal, toLight)));
                half3  plate = mark * lit * wear
                             + _GlintColor.rgb * pow(saturate(dot(normal, halfway)), _GlintPower) * _GlintIntensity * 0.4;

                // The rivet nearest this pixel, on the ring midway between two blades (90, 210 and 330 degrees).
                float  rivetAngle = UNITY_PI * 0.5 + round((a - UNITY_PI * 0.5) / third) * third;
                float2 rivet = (q - float2(cos(rivetAngle), sin(rivetAngle)) * (1.0 + _MarkRing) * 0.5) / 0.05;
                float  rivetR = length(rivet);
                float  rivetMask = 1.0 - smoothstep(0.8, 1.2, rivetR);
                float3 rivetNormal = normalize(float3(rivet * 0.8, 1.0));
                half3  rivetCol = lerp(_GoldShade.rgb, _GoldLit.rgb, saturate(dot(rivetNormal, toLight)))
                                + _GlintColor.rgb * pow(saturate(dot(rivetNormal, halfway)), 20.0) * 0.8;
                plate = lerp(plate, rivetCol, rivetMask);

                return lerp(col, plate, placard);
            }

            // How much of p the placard, the LED housing and the lamp cover: the seam light runs under them.
            float FuseCover(float2 p)
            {
                float  placard = 1.0 - smoothstep(0.97, 1.03, length(p - float2(0.0, _MarkLift)) / _MarkRadius);
                float2 h = abs(p - float2(0.0, _MarkLift - _MarkRadius - _LedGap - _LedSize.y)) - _LedSize.xy;
                float  box = length(max(h, 0.0)) + min(max(h.x, h.y), 0.0) - 0.02;
                float  housing = 1.0 - smoothstep(-0.006, 0.006, box);
                float2 lampCenter = float2(0.0, _MarkLift + _MarkRadius + _LampGap + _LampRadius);
                float  lamp = 1.0 - smoothstep(0.97, 1.03, length(p - lampCenter) / _LampRadius);
                return saturate(max(placard, max(housing, lamp)));
            }

            // Whether seven-segment segment i (a to g: top, upper right, lower right, bottom, lower left, upper left,
            // middle) is lit for a digit.
            float SegmentOn(float digit, float segment)
            {
                float d = clamp(round(digit), 0.0, 9.0);
                // The masks for 0 to 9, bit i for segment i, read without a table so every target indexes alike.
                float mask = d < 0.5 ? 63.0 : d < 1.5 ? 6.0 : d < 2.5 ? 91.0 : d < 3.5 ? 79.0 : d < 4.5 ? 102.0
                           : d < 5.5 ? 109.0 : d < 6.5 ? 125.0 : d < 7.5 ? 7.0 : d < 8.5 ? 127.0 : 111.0;
                return fmod(floor(mask / exp2(segment)), 2.0);
            }

            // A heart's signed distance, its tip at the origin and its lobes' tops near y = 1.
            float HeartDistance(float2 p)
            {
                p.x = abs(p.x);
                if (p.y + p.x > 1.0)
                {
                    float2 lobe = p - float2(0.25, 0.75);
                    return sqrt(dot(lobe, lobe)) - 0.3536;
                }

                float2 top = p - float2(0.0, 1.0);
                float2 edge = p - 0.5 * max(p.x + p.y, 0.0);
                return sqrt(min(dot(top, top), dot(edge, edge))) * sign(p.x - p.y);
            }

            float SegmentDistance(float2 q, float2 a, float2 b)
            {
                float2 ab = b - a;
                float  h  = saturate(dot(q - a, ab) / dot(ab, ab));
                return length(q - a - ab * h);
            }

            // One segment from a to b, its ends pulled in so the segments read apart: ghost holds every segment's
            // shape (the unlit glass), lit only those the digit lights.
            void Segment(float2 q, float2 a, float2 b, float index, float aa, inout float lit, inout float ghost)
            {
                const float thick = 0.16;
                float2 inset = normalize(b - a) * 0.14;
                float  seg = 1.0 - smoothstep(thick - aa, thick + aa, SegmentDistance(q, a + inset, b - inset));
                ghost = max(ghost, seg);
                lit = max(lit, seg * SegmentOn(_FuseDigit, index));
            }

            // The LED counter under the placard and the lamp over it. They shine by themselves: the glass and the
            // bezel take the scene light (tint), the digit and the lamp don't.
            // What shines by itself goes out in glow, to be added past the white cap, so it blooms.
            half3 FuseParts(half3 col, float2 p, half3 tint, out half3 glowOut)
            {
                glowOut = 0.0;
                // The LED: a rounded box of dark glass in a steel bezel, one digit behind it.
                float2 ledCenter = float2(0.0, _MarkLift - _MarkRadius - _LedGap - _LedSize.y);
                float2 d = p - ledCenter;
                float2 h = abs(d) - _LedSize.xy;
                float  box = length(max(h, 0.0)) + min(max(h.x, h.y), 0.0) - 0.02;
                float  aa = max(fwidth(box), 0.003);
                float  housing = 1.0 - smoothstep(-aa, aa, box);
                if (housing > 0.0)
                {
                    float  bezel = smoothstep(-0.035, -0.02, box);
                    half3  glass = lerp(_LedGlass.rgb, _MarkBlack.rgb * 3.0 + 0.25, bezel) * tint;
                    // The digit's cell: x from -0.5 to 0.5, y from -1 to 1, slanted a little.
                    float2 q = d / (_LedSize.y * 0.72);
                    q.x -= q.y * 0.12;
                    q.x *= 1.6;
                    float qa = max(fwidth(q.y), 0.01);
                    float lit = 0.0;
                    float ghost = 0.0;
                    Segment(q, float2(-0.5, 1.0), float2(0.5, 1.0), 0.0, qa, lit, ghost);
                    Segment(q, float2(0.5, 1.0), float2(0.5, 0.0), 1.0, qa, lit, ghost);
                    Segment(q, float2(0.5, 0.0), float2(0.5, -1.0), 2.0, qa, lit, ghost);
                    Segment(q, float2(0.5, -1.0), float2(-0.5, -1.0), 3.0, qa, lit, ghost);
                    Segment(q, float2(-0.5, -1.0), float2(-0.5, 0.0), 4.0, qa, lit, ghost);
                    Segment(q, float2(-0.5, 0.0), float2(-0.5, 1.0), 5.0, qa, lit, ghost);
                    Segment(q, float2(-0.5, 0.0), float2(0.5, 0.0), 6.0, qa, lit, ghost);

                    // Unarmed, a heart in place of the digit, and no unlit segments behind it.
                    float2 hp = d / (_LedSize.y * 1.25) + float2(0.0, 0.5);
                    float  ha = max(fwidth(hp.y), 0.01);
                    float  heart = 1.0 - smoothstep(-ha, ha, HeartDistance(hp));
                    lit = lerp(heart, lit, _FuseArmed);
                    ghost *= _FuseArmed;

                    float  bright = lerp(_LedIdle, 1.0, _FuseArmed);
                    // The heart glows the lamp's green; the count, the LED's red.
                    half3  glow = lerp(_LampGreen.rgb, _LedColor.rgb, _FuseArmed);
                    half3  led = glass + _LedColor.rgb * ghost * 0.06;
                    col = lerp(col, led, housing);
                    glowOut += glow * lit * bright * 1.4 * _FuseGlow * housing;
                }

                // The lamp: a small dome, pulsing green while it waits, red and flashing on each count once armed.
                float2 lampCenter = float2(0.0, _MarkLift + _MarkRadius + _LampGap + _LampRadius);
                float2 l = (p - lampCenter) / _LampRadius;
                float  r = length(l);
                float  la = max(fwidth(r), 0.02);
                float  waiting = 0.55 + 0.45 * sin(_Time.y * 3.0);
                float  flash = exp(-max(_Time.y - _FuseBeat, 0.0) * 5.0);
                half3  light = lerp(_LampGreen.rgb * waiting, _LampRed.rgb * (0.2 + 1.6 * flash), _FuseArmed);
                float  lamp = 1.0 - smoothstep(1.0 - la, 1.0 + la, r);
                float  spot = pow(saturate(1.0 - length(l - float2(-0.35, 0.4)) * 1.8), 3.0);
                float  inner = smoothstep(1.0, 0.75, r);
                half3  dome = lerp(_MarkBlack.rgb * 3.0 * tint, spot * 0.8, inner);
                col = lerp(col, dome, lamp);
                glowOut += light * 1.3 * _FuseGlow * inner * lamp;
                float  halo = saturate(1.0 - (r - 1.0) / 1.6);
                glowOut += light * _LampHalo * halo * halo * (1.0 - lamp);
                return col;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;

                float2 worldCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                float  worldExtent = length(float3(unity_ObjectToWorld._m00,
                                                   unity_ObjectToWorld._m10,
                                                   unity_ObjectToWorld._m20));
                OUT.worldData = float3(worldCenter, worldExtent);

                // The sphere is shaded in the sprite's own space, which turns with the balloon's sway;
                // the light isn't turned to match, so it drifts a little with the sway, accepted as on
                // ToughBalloon's grain.
                OUT.toLight = normalize(float3(SceneLightDirectionAtLOD(worldCenter), _LightHeight));
                OUT.halfway = normalize(OUT.toLight + float3(0, 0, 1));
                // The other balloons' night response, so the lead dims with the board.
                OUT.lightTint = SceneLightTintAtLOD(worldCenter) + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);

#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 spriteUV = (IN.texcoord - 0.5) / _SpriteScale + 0.5;
                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);

                fixed4 sprite = tex2D(_MainTex, spriteUV) * IN.color;
                float  alpha  = sprite.a * inBounds.x * inBounds.y;
                if (alpha < 0.01)
                {
                    discard;
                }

                float2 p      = (spriteUV - 0.5) * 2.0;
                float  rFlat  = min(length(p), 0.9999);
                float  zSph   = sqrt(1.0 - rFlat * rFlat);
                float3 normal = float3(p, zSph);

                // Plates in even steps of arc from the sphere's centre, so they crowd toward the rim.
                float  arc     = acos(zSph) / (UNITY_PI * 0.5);
                float2 plateUV = p / max(rFlat, 0.0001) * arc * _PlateScale;
                float  edgeDistance;
                float2 edgeDirection;
                float2 plate  = HexPlate(plateUV, edgeDistance, edgeDirection);
                float2 offset = plateUV - plate;

                float  plateRnd = Hash21(plate + 11.0);
                float2 tilt     = Hash22(plate + 23.0) * 2.0 - 1.0;
                float  wobble   = _Time.y * _PlateWobbleSpeed + plateRnd * BP_TAU;
                tilt = (tilt + float2(sin(wobble), cos(wobble * 1.3)) * _PlateWobble) * _PlateTilt;
                // Each balloon dents its own plates (the seed is per balloon), more of them with each hit.
                float dent = step(Hash21(plate + _VoronoiSeed.xy * 0.013), _DamageProgress * _DentShare);
                tilt = lerp(tilt, -tilt * 2.5 - normalize(plate + 0.001) * 0.25, dent);

                // The relief: each plate slopes down into its seams, so a bevel catches the light on
                // one side of the plate and falls into shade on the other.
                float  plateEdge = edgeDistance - _GapWidth;
                float  bevel  = 1.0 - smoothstep(0.0, _BevelWidth, plateEdge);
                float2 relief = edgeDirection * bevel * _BevelDepth;

                // The nearer of the plate's two bolts, one at its top corner and one at its bottom, so
                // every plate is bolted the same way corner to corner: a small dome that takes over the
                // bevel under it, so it shades and glints as a bolt head.
                float2 corner   = float2(0.0, (offset.y < 0.0 ? -0.5773503 : 0.5773503) * _BoltInset);
                float2 bolt     = (offset - corner) / _BoltRadius;
                float  boltR    = length(bolt);
                float  boltSoft = max(fwidth(boltR), 0.05);
                float  boltMask = 1.0 - smoothstep(1.0 - boltSoft, 1.0 + boltSoft, boltR);
                relief = lerp(relief, bolt * _BoltDome, boltMask);

                // A tilt along the surface; it fades toward the rim, where a plate is seen edge-on.
                float3 plateNormal = normalize(normal + float3(tilt + relief, 0) * zSph);

                float  facing = saturate(dot(plateNormal, IN.toLight));
                half3  col = lerp(_GoldShade.rgb, _GoldLit.rgb, facing);

                // The view looks straight down -z, so the reflected ray's height is 2 n.z n.y. The
                // horizon is soft over a few hundredths, and the ground and sky are the gold's own shade
                // and lit colours pushed apart a little, so the band reads without leaving the palette.
                float  reflectedUp = 2.0 * plateNormal.z * plateNormal.y;
                float  sky = smoothstep(_EnvHorizon - 0.06, _EnvHorizon + 0.06, reflectedUp);
                half3  env = lerp(_GoldShade.rgb * 0.55, _GoldLit.rgb * 1.15, sky);
                col = lerp(col, env, _EnvStrength);
                // Faded toward the rim with the tilt, where the plates are too thin to vary without shimmering.
                col *= 1.0 + (plateRnd - 0.5) * 2.0 * _PlateVariance * zSph;
                col *= 1.0 - _DentDarken * dent;
                col = lerp(col, _GoldEdge.rgb, pow(rFlat, _EdgePower));

                // Each plate's own sheen and glint, so the highlight breaks between the plates.
                float  toHalf = saturate(dot(plateNormal, IN.halfway));
                float  glint  = pow(toHalf, _GlintPower);
                col += _GoldLit.rgb * pow(toHalf, _SheenPower) * _SheenIntensity;
                col += _GlintColor.rgb * glint * _GlintIntensity;

                // Each bolt head sits in a thin dark ring.
                float boltRing = 1.0 - smoothstep(0.0, 0.2 + boltSoft, abs(boltR - 1.0));
                col = lerp(col, _BoltRingColor.rgb, boltRing);

                float edgeSoft = max(fwidth(edgeDistance), 0.002);
                float onPlate  = smoothstep(-edgeSoft, edgeSoft, plateEdge);

                // The plates' thickness: the rubber is a smaller sphere under the plate tops, and the view
                // ray is marched down through the shell between them. Where it passes through a plate
                // below its top, that is the plate's side wall. The walls show more toward the rim, where
                // the ray runs along the surface, as on a real ball. Layers blend front to back by
                // coverage, so the plate and wall edges stay antialiased.
                float  cover  = onPlate;
                half3  layers = col * onPlate;
                float2 radial = p / max(rFlat, 0.0001);
                float  rubberRadius = 1.0 - _PlateThickness;
                float  floorEdge = edgeDistance;
                float2 floorUV   = plateUV;

                UNITY_UNROLL
                for (int layer = 1; layer <= WALL_STEPS; layer++)
                {
                    float  depth  = layer / (float)WALL_STEPS;
                    float  theta  = asin(min(rFlat / lerp(1.0, rubberRadius, depth), 1.0));
                    float2 wallUV = radial * (theta / (UNITY_PI * 0.5) * _PlateScale);
                    float  wallEdge;
                    float2 wallDirection;
                    HexPlate(wallUV, wallEdge, wallDirection);
                    float  hit = smoothstep(-edgeSoft, edgeSoft, wallEdge - _GapWidth) * (1.0 - cover);
                    // A wall faces out of its plate, across the gap, tipped a little toward the viewer.
                    float3 wallNormal = normalize(float3(wallDirection, 0.35));
                    half3  wall = lerp(_GoldShade.rgb, _GoldLit.rgb, saturate(dot(wallNormal, IN.toLight)));
                    layers += wall * (1.0 - _WallShade * depth) * hit;
                    cover  += hit;
                    floorEdge = wallEdge;
                    floorUV   = wallUV;
                }

                // The balloon at the bottom of the gaps, lit as its own smooth sphere, so its shading and
                // gloss run on unbroken from gap to gap and read as one skin under the plates.
                float3 rubberNormal = normalize(float3(p, sqrt(max(rubberRadius * rubberRadius - rFlat * rFlat, 0.0))));
                half3  rubber = lerp(_RubberShade.rgb, _RubberLit.rgb, saturate(dot(rubberNormal, IN.toLight)));
                rubber += pow(saturate(dot(rubberNormal, IN.halfway)), _RubberGlossPower) * _RubberGloss;
                rubber *= 1.0 - _GapShadow * smoothstep(0.0, max(_GapWidth, 0.001), floorEdge);
                // The shadow a plate casts, found by looking from the rubber toward the light over the
                // plates' height: it shows their height even mid-balloon, where no wall is seen.
                float2 towardLight = IN.toLight.xy / max(IN.toLight.z, 0.3)
                                   * (_PlateThickness / (UNITY_PI * 0.5) * _PlateScale);
                float  casterEdge;
                float2 casterDirection;
                HexPlate(floorUV + towardLight, casterEdge, casterDirection);
                rubber *= 1.0 - _CastShadow * smoothstep(-edgeSoft, edgeSoft, casterEdge - _GapWidth);
                rubber = lerp(rubber, _RubberShade.rgb * 0.5, pow(rFlat, _EdgePower));
                col = layers + rubber * (1.0 - cover);

                // The seam light: a thin line down the middle of each gap on the rubber, and a soft glow either side
                // of it, both hidden where a plate's wall stands in front and thinned toward the rim.
                float  seamAA = max(fwidth(floorEdge), 0.002);
                float  seamLine = 1.0 - smoothstep(_SeamWidth - seamAA, _SeamWidth + seamAA, floorEdge);
                float  seamGlow = exp(-floorEdge / max(_GapWidth * 0.45, 0.001)) * _SeamHalo;
                float  seamShow = (1.0 - cover) * (1.0 - pow(rFlat, _EdgePower));
                float  seamBeat = exp(-max(_Time.y - _FuseBeat, 0.0) * 5.0);
                float  seamWaiting = 0.8 + 0.2 * sin(_Time.y * 3.0);
                half3  seamLight = lerp(_SeamGreen.rgb * seamWaiting,
                                        _SeamRed.rgb * lerp(_SeamArmedIdle, 1.0, seamBeat), _FuseArmed);
                half3  seam = seamLight * saturate(seamLine + seamGlow) * seamShow * (1.0 - FuseCover(p));

                col = DangerPlacard(col, p, normal, IN.toLight, IN.halfway);

                // World space, so the rim never turns with the sway.
                float2 worldUV = (IN.worldPos - IN.worldData.xy) / max(IN.worldData.z, 0.0001);
                float  rim = smoothstep(0.50 - _RimWidth, 0.50, length(worldUV));
                col = lerp(col, _RimColor.rgb, rim * _RimBlend);

                // A plain multiply keeps the lead's hue under coloured light. The tint is white on the
                // prefab, and kept so a renderer colour can still dim or tint the balloon.
                half3 tint = lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence) * IN.color.rgb;
                col *= tint;
                half3 fuseGlow;
                col = FuseParts(col, p, tint, fuseGlow);
                // Balloons never bloom (Plans/PLAN-HDR): the glint and glitter top out at white. The seam light and
                // the fuse's lights are the exception, so they go on after the cap, and shine through the night.
                col = SceneLightCapToWhite(col) + seam + fuseGlow;

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
}
