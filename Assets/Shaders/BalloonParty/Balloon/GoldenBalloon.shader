Shader "BalloonParty/Balloon/GoldenBalloon"
{
    // The golden reward balloon's body: a balloon (the circle sprite, lit as a sphere like
    // ToughBalloon) plated in gold. Plates are Voronoi cells laid over the sphere, so they crowd
    // toward the rim the way tiles on a ball do, with thin dark seams between them. Each plate is
    // tilted a little and sways slowly, so it shades and glints on its own as the scene light
    // moves, and glitter sparkles only where a plate's glint lands. The shadow comes from the
    // prefab's baked shadow child, as on the tough balloon.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Gold)]
        _GoldLit    ("Lit",    Color) = (1.00, 0.86, 0.40, 1)
        _GoldShade  ("Shade",  Color) = (0.62, 0.38, 0.05, 1)
        _GoldEdge   ("Edge",   Color) = (0.30, 0.16, 0.02, 1)
        _EdgePower  ("Edge Falloff", Range(1, 8)) = 3.0
        // The light's height over the board: low rakes across the ball, high lights it face-on.
        _LightHeight ("Light Height", Range(0.1, 3)) = 0.9

        [Header(Plates)]
        _PlateScale    ("Plates Across the Radius", Range(1, 8)) = 3.2
        _PlateJitter   ("Irregularity", Range(0, 1)) = 0.8
        _PlateTilt     ("Tilt", Range(0, 0.6)) = 0.16
        _PlateVariance ("Brightness Variance", Range(0, 0.5)) = 0.12
        _PlateSway     ("Sway", Range(0, 1)) = 0.35
        _PlateSwaySpeed("Sway Speed", Range(0, 2)) = 0.4

        [Header(Seams)]
        _SeamColor ("Color", Color) = (0.36, 0.20, 0.03, 1)
        _SeamWidth ("Width", Range(0, 0.2)) = 0.03
        _SeamDepth ("Depth", Range(0, 1)) = 0.6

        [Header(Glint)]
        _GlintColor     ("Color", Color) = (1.00, 0.96, 0.80, 1)
        _GlintPower     ("Sharpness", Range(4, 200)) = 32
        _GlintIntensity ("Intensity", Range(0, 4)) = 1.8
        // The broad metallic sheen under the glint, per plate too.
        _SheenPower     ("Sheen Sharpness", Range(1, 32)) = 5
        _SheenIntensity ("Sheen Intensity", Range(0, 2)) = 0.5

        [Header(Glitter)]
        _GlitterColor     ("Color", Color) = (1.00, 0.98, 0.88, 1)
        _GlitterScale     ("Density", Range(4, 80)) = 26
        _GlitterSize      ("Speck Size", Range(0.05, 0.5)) = 0.2
        _GlitterSpeed     ("Twinkle Speed", Range(0, 12)) = 4
        _GlitterSharpness ("Twinkle Sharpness", Range(1, 16)) = 6
        _GlitterChance    ("Chance", Range(0, 1)) = 0.45
        _GlitterIntensity ("Intensity", Range(0, 6)) = 2.5
        // How far the glitter reaches past the glint: it sparkles where glint x reach > 1.
        _GlitterReach     ("Reach", Range(0.5, 20)) = 5

        [Header(Rim)]
        _RimColor ("Color", Color) = (0.55, 0.36, 0.08, 1)
        _RimWidth ("Width", Range(0, 0.5)) = 0.12

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
                // Sampled once per balloon, at its centre, so the whole body is lit from one direction.
                float2 lightDir  : TEXCOORD3;
                float3 lightTint : TEXCOORD4;
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

            fixed4 _GoldLit;
            fixed4 _GoldShade;
            fixed4 _GoldEdge;
            float  _EdgePower;
            float  _LightHeight;

            float  _PlateScale;
            float  _PlateJitter;
            float  _PlateTilt;
            float  _PlateVariance;
            float  _PlateSway;
            float  _PlateSwaySpeed;

            fixed4 _SeamColor;
            float  _SeamWidth;
            float  _SeamDepth;

            fixed4 _GlintColor;
            float  _GlintPower;
            float  _GlintIntensity;
            float  _SheenPower;
            float  _SheenIntensity;

            fixed4 _GlitterColor;
            float  _GlitterScale;
            float  _GlitterSize;
            float  _GlitterSpeed;
            float  _GlitterSharpness;
            float  _GlitterChance;
            float  _GlitterIntensity;
            float  _GlitterReach;

            fixed4 _RimColor;
            float  _RimWidth;

            float  _LightInfluence;
            float  _AmbientHue;
            float  _AmbientDimming;
            float  _SpriteScale;

            float2 PlatePoint(float2 cell)
            {
                float2 jitter = float2(Hash21(cell + 3.1), Hash21(cell + 7.7)) - 0.5;
                return 0.5 + jitter * _PlateJitter;
            }

            // The plate under uv: its cell id, and the distance to its nearest seam, measured
            // across the seam rather than to the second-nearest point, so seams keep one width.
            float2 PlateCell(float2 uv, out float seamDistance)
            {
                float2 cell = floor(uv);
                float2 f    = frac(uv);

                float2 nearestCell = 0;
                float2 nearest     = 0;
                float  best        = 8.0;

                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 g = float2(x, y);
                        float2 r = g + PlatePoint(cell + g) - f;
                        float  d = dot(r, r);
                        if (d < best)
                        {
                            best        = d;
                            nearest     = r;
                            nearestCell = g;
                        }
                    }
                }

                seamDistance = 8.0;

                UNITY_UNROLL
                for (int sy = -1; sy <= 1; sy++)
                {
                    UNITY_UNROLL
                    for (int sx = -1; sx <= 1; sx++)
                    {
                        float2 g = nearestCell + float2(sx, sy);
                        float2 r = g + PlatePoint(cell + g) - f;
                        float2 between = r - nearest;
                        if (dot(between, between) > 0.00001)
                        {
                            seamDistance = min(seamDistance, dot(0.5 * (nearest + r), normalize(between)));
                        }
                    }
                }

                return cell + nearestCell;
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

                OUT.lightDir  = SceneLightDirectionAtLOD(worldCenter);
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

                // ---- Sphere ----
                float2 p      = (spriteUV - 0.5) * 2.0;
                float  rFlat  = min(length(p), 0.9999);
                float  zSph   = sqrt(1.0 - rFlat * rFlat);
                float3 normal = float3(p, zSph);
                float3 toLight = normalize(float3(IN.lightDir, _LightHeight));

                // ---- Plates: even steps of arc over the sphere, from its centre ----
                float  arc     = acos(zSph) / (UNITY_PI * 0.5);
                float2 plateUV = p / max(rFlat, 0.0001) * arc * _PlateScale;
                float  seamDistance;
                float2 plate   = PlateCell(plateUV, seamDistance);

                float  plateRnd = Hash21(plate + 11.0);
                float2 tilt     = float2(Hash21(plate + 23.0), Hash21(plate + 37.0)) * 2.0 - 1.0;
                float  sway     = _Time.y * _PlateSwaySpeed + plateRnd * BP_TAU;
                tilt = (tilt + float2(sin(sway), cos(sway * 1.3)) * _PlateSway) * _PlateTilt;
                // A tilt along the surface; it fades toward the rim, where a plate is seen edge-on.
                float3 plateNormal = normalize(normal + float3(tilt, 0) * zSph);

                // ---- Gold body: shade to lit by the plate's own facing, darkening to the edge ----
                float  facing = saturate(dot(plateNormal, toLight));
                fixed3 col = lerp(_GoldShade.rgb, _GoldLit.rgb, facing);
                col *= 1.0 + (plateRnd - 0.5) * 2.0 * _PlateVariance;
                col = lerp(col, _GoldEdge.rgb, pow(rFlat, _EdgePower));

                // ---- Sheen and glint: each plate's own highlight, so it breaks at the seams ----
                float3 halfway = normalize(toLight + float3(0, 0, 1));
                float  toHalf  = saturate(dot(plateNormal, halfway));
                float  glint   = pow(toHalf, _GlintPower);
                col += _GoldLit.rgb * pow(toHalf, _SheenPower) * _SheenIntensity;
                col += _GlintColor.rgb * glint * _GlintIntensity;

                // ---- Seams ----
                float seamSoft = max(fwidth(seamDistance), 0.002);
                float seam = 1.0 - smoothstep(_SeamWidth - seamSoft, _SeamWidth + seamSoft, seamDistance);
                col = lerp(col, _SeamColor.rgb, seam * _SeamDepth);

                // ---- Glitter, only where the glint lands and off the seams ----
                float glitter = GlitterAmountBase(p * _GlitterScale, _GlitterSize, _GlitterSpeed,
                                                  _GlitterSharpness, _GlitterChance);
                glitter *= saturate(glint * _GlitterReach) * (1.0 - seam);
                col += _GlitterColor.rgb * glitter * _GlitterIntensity;

                // ---- Rim (world space, so it never turns with the sway) ----
                float2 worldUV = (IN.worldPos - IN.worldData.xy) / max(IN.worldData.z, 0.0001);
                float  rim = smoothstep(0.50 - _RimWidth, 0.50, length(worldUV));
                col = lerp(col, _RimColor.rgb, rim * 0.6);

                // ---- Scene light: a plain multiply keeps the gold's hue under coloured light ----
                col *= lerp(float3(1.0, 1.0, 1.0), IN.lightTint, _LightInfluence);

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
}
