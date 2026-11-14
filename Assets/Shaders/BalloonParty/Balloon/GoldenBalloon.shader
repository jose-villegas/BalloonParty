Shader "BalloonParty/Balloon/GoldenBalloon"
{
    // The golden reward balloon's body: a balloon (the circle sprite, lit as a sphere like
    // ToughBalloon) plated in gold. The plates are even hexagons laid over the sphere, so they crowd
    // toward the rim the way tiles on a ball do; even, so they read as fitted plates and not as the
    // tough balloon's cracks. The plates are thick and stand apart, so the balloon's rubber shows
    // between them, deeper down, with the plates' side walls and shadows around it. Each plate is bevelled at its edges and held by two
    // bolts, and tilted a little and wobbling slowly, so it shades and glints on its own as the scene
    // light moves, and glitter sparkles only where a plate's glint lands. The shadow comes from the prefab's baked
    // shadow child, as on the tough balloon.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Gold)]
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

        [Header(Glitter)]
        _GlitterColor     ("Color", Color) = (1.00, 0.98, 0.88, 1)
        _GlitterScale     ("Density", Range(4, 80)) = 16
        _GlitterSize      ("Speck Size", Range(0.05, 0.5)) = 0.3
        _GlitterSpeed     ("Twinkle Speed", Range(0, 12)) = 4
        _GlitterSharpness ("Twinkle Sharpness", Range(1, 16)) = 6
        _GlitterChance    ("Chance", Range(0, 1)) = 0.45
        _GlitterIntensity ("Intensity", Range(0, 6)) = 4.5
        // How far the glitter reaches past the glint: it sparkles where glint x reach > 1.
        _GlitterReach     ("Reach", Range(0.5, 20)) = 6

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

            half4  _GlitterColor;
            float  _GlitterScale;
            float  _GlitterSize;
            float  _GlitterSpeed;
            float  _GlitterSharpness;
            float  _GlitterChance;
            float  _GlitterIntensity;
            float  _GlitterReach;

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
                // The other balloons' night response, so gold dims with the board instead of glowing.
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

                // Glitter only where the glint lands, on the plates and off the bolt rings. The glint is
                // broad and smooth, so this branch is coherent and skips the specks over most of the body.
                float glitterGate = saturate(glint * _GlitterReach) * onPlate * (1.0 - boltRing);
                if (glitterGate > 0.004)
                {
                    float glitter = GlitterAmountBase(p * _GlitterScale, _GlitterSize, _GlitterSpeed,
                                                      _GlitterSharpness, _GlitterChance);
                    col += _GlitterColor.rgb * glitter * glitterGate * _GlitterIntensity;
                }

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

                // World space, so the rim never turns with the sway.
                float2 worldUV = (IN.worldPos - IN.worldData.xy) / max(IN.worldData.z, 0.0001);
                float  rim = smoothstep(0.50 - _RimWidth, 0.50, length(worldUV));
                col = lerp(col, _RimColor.rgb, rim * _RimBlend);

                // A plain multiply keeps the gold's hue under coloured light. The tint is white on the
                // prefab, and kept so a renderer colour can still dim or tint the balloon.
                col *= lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence) * IN.color.rgb;
                // Balloons never bloom (Plans/PLAN-HDR): the glint and glitter top out at white.
                col = SceneLightCapToWhite(col);

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
}
