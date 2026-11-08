Shader "BalloonParty/Scenario/GrassBlades"
{
    // The grass over the ground floor on ground levels: one mesh of blades (GrassBladeMesh), each a tapered
    // five-vertex strip whose vertices all sit on the blade's root. This vertex stage places, bends and wraps
    // every blade; the fragment stage only writes the interpolated colour.
    //
    // Seen from above, a standing blade shows a short stroke leaning one way. Where a ball (or a pop, a bomb, a
    // laser) stamped the disturbance field, a blade turns toward the push and lies down, so it shows its full
    // length and catches the sun. It bends to the grass wake (GrassWakeBuffer), not the field itself: the wake
    // keeps only what the field gained each frame and lets it go fast, so a blade answers the ball at once and
    // stands back up within a fraction of a second. Blades thin out over the floor's dirt patches. Per vertex:
    // one wake tap, two patch-noise taps (colour, dirt) and the scene-light field; nothing per pixel.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Patch Noise (R, linear)", 2D) = "gray" {}

        [Header(Colour)]
        _RootColor      ("Root Colour",                  Color)            = (0.16, 0.22, 0.13, 1)
        _TipColor       ("Tip Colour",                   Color)            = (0.40, 0.50, 0.27, 1)
        _DryTipColor    ("Dry Tip Colour",               Color)            = (0.52, 0.50, 0.30, 1)
        // How far the drier patches of the floor turn the tips toward the dry colour.
        _PatchDryness   ("Patch Dryness",                Range(0, 1))      = 0.7
        _ShadeJitter    ("Per-blade Shade Jitter",       Range(0, 0.5))    = 0.18

        [Header(Shape)]
        _BladeLength    ("Blade Length (world units)",   Range(0.02, 0.5)) = 0.28
        _LengthJitter   ("Length Jitter",                Range(0, 1))      = 0.45
        _BladeWidth     ("Root Width (world units)",     Range(0.005, 0.08)) = 0.05
        _LeanAngle      ("Rest Lean Angle (degrees)",    Range(0, 360))    = 90
        _LeanJitter     ("Lean Jitter (degrees)",        Range(0, 180))    = 35
        // How much of its length a standing blade shows from above; a blade laid flat shows all of it.
        _RestTilt       ("Rest Tilt",                    Range(0.05, 1))   = 0.5

        [Header(Idle Sway)]
        _SwayAmount     ("Sway (degrees)",               Range(0, 45))     = 9
        _SwaySpeed      ("Sway Speed",                   Range(0, 6))      = 1.6
        // Length of the gust waves rolling across the meadow.
        _SwayWave       ("Sway Wave Length (world units)", Float)          = 2.5

        [Header(Wake)]
        // How quickly the wake's push turns a blade toward it and lays it down: the response saturates, so
        // higher means a lighter push (the leading edge of the ball's wake) already moves the blade most of the way.
        _WakeBend       ("Wake Response",                Range(0, 12))     = 5
        // How far a stirred wake lays the blades flat.
        _WakeFlatten    ("Wake Flatten",                 Range(0, 3))      = 1.6
        _WakeSheen      ("Wake Sheen",                   Range(0, 1))      = 0.35
        _SheenColor     ("Sheen Colour",                 Color)            = (0.62, 0.68, 0.42, 1)

        [Header(Lighting)]
        // Scene-light colour x intensity multiplies the blades: 0 = authored colours always, 1 = fully lit.
        _LightInfluence ("Light Influence",              Range(0, 1))      = 1
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
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/Ground.cginc"

            sampler2D _NoiseTex;
            half4  _RootColor;
            half4  _TipColor;
            half4  _DryTipColor;
            half   _PatchDryness;
            half   _ShadeJitter;
            float  _BladeLength;
            half   _LengthJitter;
            float  _BladeWidth;
            float  _LeanAngle;
            float  _LeanJitter;
            half   _RestTilt;
            float  _SwayAmount;
            float  _SwaySpeed;
            float  _SwayWave;
            half   _WakeBend;
            half   _WakeFlatten;
            half   _WakeSheen;
            half4  _SheenColor;
            half   _LightInfluence;

            // Per renderer (GroundView's property block): xy = the tile's world min, zw = its size.
            float4 _BladeTile;

            // The wake from GrassWakeBuffer (rg = push, b = stir), in the disturbance field's UV space; the field
            // bounds are DisturbanceFieldService's globals. The ground's own globals come from Ground.cginc.
            sampler2D _GrassWakeTex;
            float2    _FieldBoundsMin;
            float2    _FieldBoundsSize;

            struct appdata_t
            {
                // xy = the blade's root as a fraction of the tile (UNorm16); every vertex of a blade carries it.
                float4 vertex  : POSITION;
                // x = side across the blade (-1 / +1, 0 at the tip), y = how far along it (0 root, 1 tip). SNorm8.
                float4 corner  : TEXCOORD0;
                // Per-blade randoms in 0..1 (UNorm8): x = lean, y = length, z = shade, w = sway phase.
                float4 random  : TEXCOORD1;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // TEXCOORD, not COLOR: some mobile drivers clamp COLOR to 0..1, and the sheen and the light can pass 1.
                half3  color  : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                // Wrap the root across the tile as the meadow scrolls: the blade planted at ground point g draws at
                // g + scroll, folded back into the tile, which reaches past every edge the camera shows.
                float2 tileMin = _BladeTile.xy;
                float2 tileSize = max(_BladeTile.zw, 1e-3);
                float2 root = tileMin + frac(IN.vertex.xy + (_GroundScroll - tileMin) / tileSize) * tileSize;
                float2 ground = root - _GroundScroll;

                // The wake covers the camera's rect, like the field; blades outside it rest.
                float2 fieldUV = (root - _FieldBoundsMin) / max(_FieldBoundsSize, 1e-4);
                float inField = all(fieldUV == saturate(fieldUV)) ? 1.0 : 0.0;
                float3 wake = tex2Dlod(_GrassWakeTex, float4(fieldUV, 0.0, 0.0)).rgb;
                float2 push = wake.rg * inField;
                float stir = saturate(wake.b) * inField;

                float patch = tex2Dlod(_NoiseTex, float4(GroundPatchUV(ground), 0.0, 0.0)).r;

                // Across a dirt patch's fade the blades thin out (each drops out at its own point, so the grass
                // frays rather than ending on a line) and the survivors shorten; inside it they collapse onto the
                // root and draw nothing.
                float dirt = GroundDirtMask(tex2Dlod(_NoiseTex, float4(GroundDirtUV(ground), 0.0, 0.0)).r);
                float grows = step(dirt, IN.random.w * 0.9 + 0.05) * (1.0 - dirt * 0.6);

                // Rest lean plus gust waves rolling across the ground, so neighbours sway together.
                float wave = dot(ground, float2(0.6, 0.8)) / max(_SwayWave, 1e-3) * 6.2832;
                float sway = sin(_Time.y * _SwaySpeed - wave + IN.random.w * 1.5) * _SwayAmount;
                float angle = radians(_LeanAngle + (IN.random.x - 0.5) * 2.0 * _LeanJitter + sway);
                float2 restDir = float2(cos(angle), sin(angle));

                // The push turns the blade toward it and lays it down: from above, more of its length shows. The
                // response saturates rather than following the push linearly, so a blade answers the first light
                // touch of the wake instead of waiting for the push to build.
                // rsqrt with a floor: a push exactly against the rest lean would otherwise normalize zero.
                float pushLength = length(push);
                float response = 1.0 - exp2(-pushLength * _WakeBend);
                float2 bend = lerp(restDir, push / max(pushLength, 1e-4), response);
                float2 tipDir = bend * rsqrt(max(dot(bend, bend), 1e-6));
                float flat = saturate(response + stir * _WakeFlatten);
                float tilt = lerp(_RestTilt, 1.0, flat);
                float bladeLength = _BladeLength * (1.0 - _LengthJitter * IN.random.y) * grows;

                // A quadratic curve: the stem leaves the root along its rest lean, the tip ends where it is pushed.
                float t = IN.corner.y;
                float2 control = restDir * (bladeLength * _RestTilt * 0.5);
                float2 tip = tipDir * (bladeLength * tilt);
                float2 along = 2.0 * (1.0 - t) * t * control + t * t * tip;
                float2 tangent = normalize(2.0 * (1.0 - t) * control + 2.0 * t * (tip - control) + restDir * 1e-4);
                float2 across = float2(-tangent.y, tangent.x) * (IN.corner.x * _BladeWidth * 0.5 * (1.0 - t) * grows);

                float2 world = root + along + across;
                OUT.vertex = mul(UNITY_MATRIX_VP, float4(world, 0.0, 1.0));

                // Blades dry out toward the dirt, so the colour fades into it before the grass thins away.
                half dryness = saturate((patch - 0.5) * 2.0 * _PatchDryness + 0.5 * _PatchDryness + dirt * 1.5);
                half3 tipColor = lerp(_TipColor.rgb, _DryTipColor.rgb, dryness);
                half3 color = lerp(_RootColor.rgb, tipColor, (half)t);
                color *= 1.0h + ((half)IN.random.z - 0.5h) * 2.0h * _ShadeJitter;

                // Laid-down blades show their sides: brighter toward the tip where they lean toward the sun.
                // Field-aware, one sample per blade at its root, like the sprites' VTF light: the time of day plus
                // the local light of pops and items.
                half facing = saturate(dot(tipDir, SceneLightDirectionAtLOD(root)) * 0.5 + 0.5);
                color += _SheenColor.rgb * (half)(flat * t) * facing * _WakeSheen;

                half3 light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTintAtLOD(root), _LightInfluence);
                OUT.color = color * light;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                return half4(IN.color, 1.0h);
            }
            ENDCG
        }
    }
}
