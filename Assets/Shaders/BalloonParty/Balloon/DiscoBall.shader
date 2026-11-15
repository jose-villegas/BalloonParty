Shader "BalloonParty/Balloon/DiscoBall"
{
    // The disco ball reward balloon's body (PLAN-DiscoBalloon): a balloon (the circle sprite, lit as a sphere
    // like GoldenBalloon) tiled with small flat mirrors on a latitude-longitude grid, narrower toward the poles,
    // with dark grout between them. The grid turns slowly about the balloon's up, so tiles slide across the face
    // and over the limb. Each tile is flat (one normal, its centre's, tipped a little at random), which is what
    // makes it read as a mosaic and not as chrome: it reflects its own piece of the board from the shared scene
    // capture (_SceneCaptureTex, SceneCaptureService), and the pieces jump from tile to tile. The base silver is
    // lit by the scene light at the ball, so it is bright at noon and dim at night; a tile whose normal turns
    // through the light's angle flashes, and the glitter sparkles only on those flashes. Three cues give it depth
    // under the reflection: the whole ball, mirrors and all, is shaded as one sphere (a lit side and a shadow side);
    // each tile also mirrors a fixed sky and floor (bright above the horizon, dark below), so the tiles darken
    // toward the bottom and the limb; and each tile is bevelled, its edges catching or losing the light.
    // DiscoBallLook pushes the per-instance spin phase and holds the capture while a ball is on screen.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Tiles)]
        // Rows from pole to pole; a row's tiles are as wide as they are tall, so there are fewer toward the poles.
        _TileRows    ("Rows", Range(4, 24)) = 11
        // Half the grout between two tiles, in tile units.
        _Grout       ("Grout", Range(0, 0.3)) = 0.08
        _GroutColor  ("Grout Colour", Color) = (0.08, 0.08, 0.10, 1)
        _TileTilt    ("Tile Tilt", Range(0, 0.4)) = 0.12
        _TileVariance ("Brightness Variance", Range(0, 0.5)) = 0.4
        // The bevel round each tile, in tile units, and how much its slope lights or darkens the edge.
        _BevelWidth    ("Bevel Width", Range(0, 0.45)) = 0.2
        _BevelContrast ("Bevel Contrast", Range(0, 3)) = 1.6
        // Radians per second about the balloon's up.
        _SpinSpeed   ("Spin Speed", Range(0, 2)) = 0.35
        [PerRendererData] _SpinPhase ("Spin Phase", Float) = 0

        [Header(Silver)]
        _SilverLit   ("Lit", Color) = (0.86, 0.88, 0.92, 1)
        _SilverShade ("Shade", Color) = (0.30, 0.32, 0.36, 1)
        _EdgeColor   ("Edge", Color) = (0.10, 0.10, 0.12, 1)
        _EdgePower   ("Edge Falloff", Range(1, 8)) = 3.5
        _LightHeight ("Light Height", Range(0.1, 3)) = 0.9
        // The ball as one sphere: how dark its far side gets, and how far the light wraps round it.
        _FormShade   ("Form Shade", Range(0, 1)) = 0.45
        _FormWrap    ("Form Wrap", Range(0, 1)) = 0.35

        [Header(Reflection)]
        _ReflectionStrength ("Strength", Range(0, 1)) = 0.75
        // How much of the board around the ball a tile can show, in ball radii.
        _ReflectionReach    ("Reach (radii)", Range(0.5, 8)) = 3
        // How much of its piece a tile shows across itself: 0 is one flat colour per tile.
        _ReflectionDetail   ("Detail Within a Tile", Range(0, 1)) = 0.3
        // A fixed sky and floor every tile mirrors under the board's piece: bright above the horizon, dark below.
        _EnvSky      ("Sky", Color) = (1.00, 1.00, 1.00, 1)
        _EnvFloor    ("Floor", Color) = (0.12, 0.12, 0.15, 1)
        _EnvStrength ("Sky And Floor Strength", Range(0, 1)) = 0.35
        _EnvHorizon  ("Horizon", Range(-0.8, 0.8)) = -0.1

        [Header(Flash)]
        _FlashColor     ("Colour", Color) = (1.00, 1.00, 1.00, 1)
        _FlashPower     ("Sharpness", Range(8, 400)) = 40
        _FlashIntensity ("Intensity", Range(0, 4)) = 2.5
        _SheenPower     ("Sheen Sharpness", Range(1, 32)) = 6
        _SheenIntensity ("Sheen Intensity", Range(0, 2)) = 0.6

        [Header(Glitter)]
        _GlitterColor     ("Colour", Color) = (1.00, 1.00, 1.00, 1)
        _GlitterScale     ("Density", Range(4, 80)) = 22
        _GlitterSize      ("Speck Size", Range(0.05, 0.5)) = 0.3
        _GlitterSpeed     ("Twinkle Speed", Range(0, 12)) = 5
        _GlitterSharpness ("Twinkle Sharpness", Range(1, 16)) = 6
        _GlitterChance    ("Chance", Range(0, 1)) = 0.5
        _GlitterIntensity ("Intensity", Range(0, 6)) = 4
        _GlitterReach     ("Reach", Range(0.5, 20)) = 5

        [Header(Rim)]
        _RimColor ("Colour", Color) = (0.20, 0.20, 0.24, 1)
        _RimWidth ("Width", Range(0, 0.5)) = 0.1
        _RimBlend ("Blend", Range(0, 1)) = 0.5

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

            #include "UnityCG.cginc"
            #include "../Include/MathConst.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/Glitter.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 worldPos  : TEXCOORD1;
                // xy = world-space object centre, z = world-space extent (X scale).
                float3 worldData : TEXCOORD2;
                // The light is sampled once, at the ball's centre.
                float3 toLight   : TEXCOORD3;
                float3 halfway   : TEXCOORD4;
                float3 lightTint : TEXCOORD5;
                // xy = the centre on the scene capture, zw = one ball radius there.
                float4 capture   : TEXCOORD6;
            };

            sampler2D _MainTex;
            sampler2D _SceneCaptureTex;
            fixed4 _RendererColor;
            fixed4 _Color;

            float  _TileRows;
            float  _Grout;
            half4  _GroutColor;
            float  _TileTilt;
            float  _TileVariance;
            float  _BevelWidth;
            float  _BevelContrast;
            float  _SpinSpeed;
            float  _SpinPhase;

            half4  _SilverLit;
            half4  _SilverShade;
            half4  _EdgeColor;
            float  _EdgePower;
            float  _LightHeight;
            float  _FormShade;
            float  _FormWrap;

            float  _ReflectionStrength;
            float  _ReflectionReach;
            float  _ReflectionDetail;
            half4  _EnvSky;
            half4  _EnvFloor;
            float  _EnvStrength;
            float  _EnvHorizon;

            half4  _FlashColor;
            float  _FlashPower;
            float  _FlashIntensity;
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

            float2 Hash22(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.xx + p3.yz) * p3.zy);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;

                float3 worldCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float  worldExtent = length(float3(unity_ObjectToWorld._m00,
                                                   unity_ObjectToWorld._m10,
                                                   unity_ObjectToWorld._m20));
                OUT.worldData = float3(worldCenter.xy, worldExtent);

                OUT.toLight   = normalize(float3(SceneLightDirectionAtLOD(worldCenter.xy), _LightHeight));
                OUT.halfway   = normalize(OUT.toLight + float3(0, 0, 1));
                OUT.lightTint = SceneLightTintAtLOD(worldCenter.xy) + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);

                // Where the ball sits on the capture, and how far one radius reaches there, the way
                // UnbreakableBalloon maps its mirror onto it.
                float  radius  = worldExtent * 0.5 * _SpriteScale;
                float4 center  = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(worldCenter, 1.0)));
                float4 acrossX = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(worldCenter + float3(radius, 0, 0), 1.0)));
                float4 acrossY = ComputeScreenPos(mul(UNITY_MATRIX_VP, float4(worldCenter + float3(0, radius, 0), 1.0)));
                float2 centerUV = center.xy / center.w;
                OUT.capture = float4(centerUV, acrossX.x / acrossX.w - centerUV.x, acrossY.y / acrossY.w - centerUV.y);

#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
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

                // Latitude from pole to pole, longitude about the up axis, turned by the spin.
                float spin  = _Time.y * _SpinSpeed + _SpinPhase;
                float lat   = asin(clamp(normal.y, -1.0, 1.0));
                float lon   = atan2(normal.x, normal.z) + spin;
                float rows  = floor(_TileRows);
                float v     = (lat + UNITY_PI * 0.5) / UNITY_PI * rows;
                float row   = floor(v);
                float rowLat = (row + 0.5) / rows * UNITY_PI - UNITY_PI * 0.5;
                // Square tiles: a row's circumference over the tile height, whole, so the row closes on itself.
                float cols  = max(3.0, floor(2.0 * rows * cos(rowLat) + 0.5));
                float u     = lon / BP_TAU * cols;
                float col   = floor(u);

                float2 tile    = float2(col - cols * floor(col / cols), row);
                float  tileRnd = Hash21(tile + 17.0);
                float2 tilt    = (Hash22(tile + 5.0) * 2.0 - 1.0) * _TileTilt;

                // The tile's own flat normal: its centre's, turned back by the spin into view, tipped at random.
                float  tileLon = (col + 0.5) / cols * BP_TAU - spin;
                float3 tileNormal = float3(cos(rowLat) * sin(tileLon), sin(rowLat), cos(rowLat) * cos(tileLon));
                tileNormal = normalize(tileNormal + float3(tilt, 0.0));

                // Grout: the distance to the tile's nearest edge, in tile units.
                float2 inTile = float2(frac(u), frac(v));
                float  edge   = min(min(inTile.x, 1.0 - inTile.x), min(inTile.y, 1.0 - inTile.y));
                float  edgeSoft = max(fwidth(v), 0.002);
                float  onTile = smoothstep(_Grout - edgeSoft, _Grout + edgeSoft, edge);

                // The bevel: near an edge the tile slopes down toward it, so its normal leans out along the
                // sphere's east (across the row) or north (up the rows), whichever edge is nearer.
                float3 east  = float3(cos(tileLon), 0.0, -sin(tileLon));
                float3 north = float3(-sin(rowLat) * sin(tileLon), cos(rowLat), -sin(rowLat) * cos(tileLon));
                float2 fromCentre = inTile - 0.5;
                float3 lean = abs(fromCentre.x) > abs(fromCentre.y)
                            ? east * sign(fromCentre.x)
                            : north * sign(fromCentre.y);
                float  bevel = 1.0 - smoothstep(_Grout, _Grout + max(_BevelWidth, 1e-3), edge);
                float3 bevelNormal = normalize(tileNormal + lean * bevel);

                // The base silver, lit by the scene light.
                float facing = saturate(dot(tileNormal, IN.toLight));
                half3 col3 = lerp(_SilverShade.rgb, _SilverLit.rgb, facing);
                col3 *= 1.0 + (tileRnd - 0.5) * 2.0 * _TileVariance;

                // Every tile also mirrors a fixed sky and floor, by where its reflection points.
                float  reflectUp = 2.0 * tileNormal.z * tileNormal.y;
                half3  env = lerp(_EnvFloor.rgb, _EnvSky.rgb, smoothstep(_EnvHorizon - 0.25, _EnvHorizon + 0.25, reflectUp));
                col3 = lerp(col3, env, _EnvStrength);
                col3 *= lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence);

                // The view looks straight down -z, so a mirror sends it along (2 n.z n.x, 2 n.z n.y). A tile
                // shows its piece of the board around the ball; the capture is already lit by the scene, so
                // it isn't lit again. A tile tipped past the limb would flip its piece to the far side of the
                // board, so its facing is held just above edge-on.
                float2 reflected = 2.0 * max(tileNormal.z, 0.05) * tileNormal.xy;
                float2 captureUV = IN.capture.xy
                                 + (reflected * _ReflectionReach + p * _ReflectionDetail) * IN.capture.zw;
                float2 edgeFade  = smoothstep(0.0, 0.02, captureUV) * smoothstep(0.0, 0.02, 1.0 - captureUV);
                half3  mirror    = tex2D(_SceneCaptureTex, saturate(captureUV)).rgb;
                col3 = lerp(col3, mirror, _ReflectionStrength * edgeFade.x * edgeFade.y * zSph);

                // A tile turning through the light's angle flashes whole; the flash follows the light's colour,
                // so it dims at night with the board.
                float toHalf = saturate(dot(tileNormal, IN.halfway));
                float flash  = pow(toHalf, _FlashPower);
                half3 lightColour = lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence);
                col3 += _SilverLit.rgb * pow(toHalf, _SheenPower) * _SheenIntensity * lightColour;
                col3 += _FlashColor.rgb * flash * _FlashIntensity * lightColour;

                float glitterGate = saturate(flash * _GlitterReach) * onTile;
                if (glitterGate > 0.004)
                {
                    float glitter = GlitterAmountBase(p * _GlitterScale, _GlitterSize, _GlitterSpeed,
                                                      _GlitterSharpness, _GlitterChance);
                    col3 += _GlitterColor.rgb * glitter * glitterGate * _GlitterIntensity * lightColour;
                }

                // The bevel's slope against the light: the edge facing it brightens, the far one darkens.
                col3 *= saturate(1.0 + (dot(bevelNormal, IN.toLight) - dot(tileNormal, IN.toLight)) * _BevelContrast);

                col3 = lerp(_GroutColor.rgb * lightColour, col3, onTile);

                // The ball as one sphere, mirrors and all, wrapped so the shadow side isn't black.
                float form = saturate((dot(normal, IN.toLight) + _FormWrap) / (1.0 + _FormWrap));
                col3 *= lerp(_FormShade, 1.0, form);
                col3 = lerp(col3, _EdgeColor.rgb, pow(rFlat, _EdgePower));

                // World space, so the rim never turns with the sway.
                float2 worldUV = (IN.worldPos - IN.worldData.xy) / max(IN.worldData.z, 0.0001);
                float  rim = smoothstep(0.50 - _RimWidth, 0.50, length(worldUV));
                col3 = lerp(col3, _RimColor.rgb, rim * _RimBlend);

                col3 *= IN.color.rgb;
                // Balloons never bloom (Plans/PLAN-HDR): the flashes and glitter top out at white.
                col3 = SceneLightCapToWhite(col3);

                return fixed4(col3 * alpha, alpha);
            }
            ENDCG
        }
    }
}
