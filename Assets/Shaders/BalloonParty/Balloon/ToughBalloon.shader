Shader "BalloonParty/Balloon/ToughBalloon"
{
    Properties
    {
        // ---- Sprite --------------------------------------------------------
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)

        // ---- Damage (driven from C#) ---------------------------------------
        [Header(Damage)]
        _DamageProgress ("Progress", Range(0,1)) = 0
        [Space(2)]
        // < 1 fast early / slow late  |  1 linear  |  > 1 slow early / dramatic late
        _DamageCurve ("Curve  (0.5 sqrt · 1 linear · 2 square)", Range(0.3, 3.0)) = 1.8
        [GradientTexture] _AshGradient   ("Ash Gradient",   2D) = "white" {}
        [GradientTexture] _CrackGradient ("Crack Gradient", 2D) = "white" {}

        // ---- Rim -----------------------------------------------------------
        [Header(Rim)]
        _RimColor ("Color", Color) = (0.18, 0.18, 0.22, 1)
        _RimWidth ("Width", Range(0, 0.5)) = 0.11

        // ---- Surface grain (leather-like bumps) --------------------------------
        [Header(Surface Grain)]
        _GrainScale ("Scale", Range(2, 80)) = 20
        _GrainStrength ("Strength", Range(0, 1.0)) = 0.25

        // ---- Voronoi cracks ------------------------------------------------
        [Header(Cracks  Base)]
        _CrackThreshold ("Edge Threshold", Range(0.02, 0.15)) = 0.08
        _CrackSharpness ("Sharpness", Range(5, 60)) = 28

        [Header(Cracks  Fibers)]
        _FiberDensity ("Fiber Density", Range(4, 40)) = 16
        _FiberThickness ("Fiber Thickness", Range(0.01, 15.0)) = 1.0
        _FiberIntensity ("Fiber Intensity", Range(0, 1)) = 0.6
        _FiberColor ("Fiber Color", Color) = (0.06, 0.05, 0.05, 1)

        [Header(Cracks  Sphere Projection)]
        _SphereWarp ("Warp Strength", Range(1, 6)) = 2.5
        _SphereWarpDamageBoost ("Warp Damage Boost", Range(0, 6)) = 2.0
        _VoronoiScale ("Cell Scale", Range(2, 12)) = 4.5
        _VoronoiScaleDamageBoost ("Cell Scale Damage Boost", Range(0, 12)) = 3.0

        [Header(Cracks  Instance)]
        [HideInInspector] _VoronoiSeed ("Voronoi Seed (set at runtime)", Vector) = (0, 0, 0, 0)

        // ---- Shadow --------------------------------------------------------
        // Inverted keyword so materials serialized before the toggle existed keep their
        // shadow: an absent keyword means "shadow on".
        [Header(Shadow)]
        [Toggle(_SHADOW_OFF)] _DisableShadow ("Disable (use a baked shadow instead)", Float) = 0
        _ShadowColor    ("Color",    Color)             = (0.2, 0.2, 0.2, 0.75)
        _ShadowOffset   ("Offset",   Vector)            = (0.025, -0.025, 0, 0)
        _ShadowSoftness ("Softness", Range(0.0, 0.1))   = 0.01
        _SpriteScale    ("Sprite Scale", Range(0.1, 1.0)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
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
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing
            #pragma shader_feature_local _SHADOW_OFF
            #include "UnityCG.cginc"

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
                // xy = world-space object center, z = world-space extent (X scale)
                // Computed in vertex to avoid per-fragment matrix work.
                float3 worldData : TEXCOORD2;
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

            float     _DamageProgress;
            float     _DamageCurve;
            sampler2D _AshGradient;
            sampler2D _CrackGradient;

            fixed4 _RimColor;
            float  _RimWidth;

            float  _GrainScale;
            float  _GrainStrength;

            // Global shader property — set by SceneLightService, not in Properties so
            // material values can't mask it. Points TOWARD the light, normalized;
            // canonical (-0.707, 0.707) = upper-left.
            float4 _SceneLightDir;

            float  _VoronoiScale;
            float  _VoronoiScaleDamageBoost;
            float  _SphereWarp;
            float  _SphereWarpDamageBoost;
            float2 _VoronoiSeed;
            float  _CrackThreshold;
            float  _CrackSharpness;
            float  _FiberDensity;
            float  _FiberThickness;
            float  _FiberIntensity;
            fixed4 _FiberColor;

            fixed4 _ShadowColor;
            float2 _ShadowOffset;
            float  _ShadowSoftness;
            float  _SpriteScale;

            // ----------------------------------------------------------------
            // Grain - faked leather micro-bumps via smooth value noise
            // ----------------------------------------------------------------
            float GrainHash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Smooth value noise with bilinear interpolation for visible bumps.
            float GrainNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                // Smooth hermite for rounded bumps
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = GrainHash(i);
                float b = GrainHash(i + float2(1, 0));
                float c = GrainHash(i + float2(0, 1));
                float d = GrainHash(i + float2(1, 1));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            // Returns a fake NdotL bump value from the noise gradient,
            // using the sphere normal to orient the shading.
            float LeatherGrain(float2 uv, float3 sphereNormal, float scale, float2 lightDir)
            {
                float2 suv = uv * scale;
                float eps  = 0.5;
                float h    = GrainNoise(suv);
                float hx   = GrainNoise(suv + float2(eps, 0));
                float hy   = GrainNoise(suv + float2(0, eps));
                float2 grad = float2(hx - h, hy - h) / eps;

                // Scale gradient by surface facing — bumps flatten at sphere edges
                float facing = max(sphereNormal.z, 0.0);
                return dot(normalize(lightDir), grad) * facing;
            }

            // ----------------------------------------------------------------
            // Voronoi helpers
            // ----------------------------------------------------------------
            float2 VoronoiHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            // Returns (nearest distance, second-nearest distance).
            // The difference (y - x) is strongest along cell edges.
            float2 Voronoi(float2 uv)
            {
                float2 cell = floor(uv);
                float2 f    = frac(uv);

                float d0 = 8.0, d1 = 8.0;
                float2 nearestPt = 0;
                float2 secondPt  = 0;

                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 n   = float2(x, y);
                        float2 pt  = VoronoiHash(cell + n);
                        float2 dv  = n + pt - f;
                        float  d   = dot(dv, dv);

                        if (d < d0) { d1 = d0; secondPt = nearestPt; d0 = d; nearestPt = dv; }
                        else if (d < d1) { d1 = d; secondPt = dv; }
                    }
                }

                return float2(sqrt(d0), sqrt(d1));
            }

            // Voronoi with edge direction output for fiber orientation.
            float2 VoronoiWithDir(float2 uv, out float2 edgeDir)
            {
                float2 cell = floor(uv);
                float2 f    = frac(uv);

                float d0 = 8.0, d1 = 8.0;
                float2 nearestPt = 0;
                float2 secondPt  = 0;

                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 n   = float2(x, y);
                        float2 pt  = VoronoiHash(cell + n);
                        float2 dv  = n + pt - f;
                        float  d   = dot(dv, dv);

                        if (d < d0) { d1 = d0; secondPt = nearestPt; d0 = d; nearestPt = dv; }
                        else if (d < d1) { d1 = d; secondPt = dv; }
                    }
                }

                // Edge direction runs between the two nearest cell points;
                // fibers stretch perpendicular to the crack (along the bridge).
                edgeDir = normalize(secondPt - nearestPt);
                return float2(sqrt(d0), sqrt(d1));
            }

            // 1D hash for fiber fragment placement.
            float FiberHash(float p)
            {
                return frac(sin(p * 127.1) * 43758.5453);
            }

            // 2D hash returning 0-1.
            float FiberHash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            // Generates scattered fiber fragments like torn leather across cracks.
            // crackWidth: normalized crack opening (0 = closed, 1 = fully open)
            float RubberFibers(float2 uv, float2 edgeDir, float density, float thickness, float seed, float crackWidth)
            {
                float2 scaled = uv * density;
                float2 cell   = floor(scaled);
                float2 f      = frac(scaled) - 0.5;

                // Perpendicular to crack edge - fibers bridge across
                float2 bridgeDir = float2(-edgeDir.y, edgeDir.x);
                float baseAngle  = atan2(bridgeDir.y, bridgeDir.x);

                // Crack width drives fragment size and spread
                float sizeScale = lerp(0.3, 1.5, crackWidth);

                float result = 0.0;

                UNITY_UNROLL
                for (int oy = -1; oy <= 1; oy++)
                {
                    UNITY_UNROLL
                    for (int ox = -1; ox <= 1; ox++)
                    {
                        float2 neighbor = float2(ox, oy);
                        float2 nc       = cell + neighbor;

                        float h1 = FiberHash2(nc + seed * 5.3);
                        float h2 = FiberHash2(nc * 1.73 + seed * 3.1);
                        float h3 = FiberHash2(nc * 2.91 + seed * 7.7);
                        float h4 = FiberHash2(nc * 0.67 + seed * 11.3);

                        float2 pos   = neighbor + float2(h2 - 0.5, h3 - 0.5) * 0.7;
                        float2 delta = f - pos;

                        // Tight alignment to bridge direction with slight wobble
                        float angle = baseAngle + (h4 - 0.5) * 0.5;
                        float2 dir  = float2(cos(angle), sin(angle));
                        float2 perp = float2(-dir.y, dir.x);

                        float along  = abs(dot(delta, dir));
                        float across = abs(dot(delta, perp));

                        // Use noise to vary thickness per fragment
                        float noiseThick = GrainNoise(nc * 3.7 + seed) * 0.7 + 0.5;

                        float fragLen   = lerp(0.25, 0.55, h2) * thickness * noiseThick * sizeScale;
                        float fragWidth = fragLen * lerp(0.06, 0.18, h3) * noiseThick * sizeScale;

                        float shape = smoothstep(fragLen, fragLen * 0.3, along)
                                    * smoothstep(fragWidth, fragWidth * 0.15, across);

                        // More cells spawn fibers as cracks widen
                        float spawnChance = lerp(0.2, 0.5, crackWidth);
                        result = max(result, shape * step(h1, spawnChance));
                    }
                }

                return result;
            }

            // ----------------------------------------------------------------
            // Shadow helpers (matches SpriteShadow 9-tap box blur)
            // ----------------------------------------------------------------
            inline fixed SampleShadowAlpha(float2 uv)
            {
                // Discard taps that fall outside the sprite quad to prevent
                // cross-shaped bleed from texture border clamping.
                float2 inBounds = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inBounds.x * inBounds.y;
            }

            inline fixed SoftShadowAlpha(float2 shadowUV, float s)
            {
                fixed a =
                    SampleShadowAlpha(shadowUV + float2(-s, -s)) +
                    SampleShadowAlpha(shadowUV + float2( 0, -s)) +
                    SampleShadowAlpha(shadowUV + float2( s, -s)) +
                    SampleShadowAlpha(shadowUV + float2(-s,  0)) +
                    SampleShadowAlpha(shadowUV                 ) +
                    SampleShadowAlpha(shadowUV + float2( s,  0)) +
                    SampleShadowAlpha(shadowUV + float2(-s,  s)) +
                    SampleShadowAlpha(shadowUV + float2( 0,  s)) +
                    SampleShadowAlpha(shadowUV + float2( s,  s));
                return a / 9.0;
            }

            // ----------------------------------------------------------------
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

#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // Scale sprite UV inward so the quad has transparent margins for the shadow
                float2 spriteUV = (IN.texcoord - 0.5) / _SpriteScale + 0.5;
                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                fixed4 sprite = tex2D(_MainTex, spriteUV) * IN.color;
                sprite.a *= spriteMask;
                float  alpha  = sprite.a;

                // ---- Shadow (sampled from scaled UV, shifted by offset) ----
                // The disabled variant compiles the taps out entirely — a baked shadow child
                // (SpriteShadowBaker) provides the shadow instead.
#ifdef _SHADOW_OFF
                fixed  shadowAlpha = 0;
                fixed3 shadowRGB   = fixed3(0, 0, 0);
#else
                float2 shadowUV = spriteUV - _ShadowOffset;
                fixed shadowAlpha = _ShadowSoftness < 0.0001
                    ? SampleShadowAlpha(shadowUV)
                    : SoftShadowAlpha(shadowUV, _ShadowSoftness);
                shadowAlpha *= IN.color.a * _ShadowColor.a;

                fixed3 shadowRGB = _ShadowColor.rgb * IN.color.rgb;
#endif

                // Early discard when both sprite and shadow are invisible
                fixed combinedA = alpha + shadowAlpha * (1.0 - alpha);
                if (combinedA < 0.01) discard;

                // ---- Balloon shading (only when sprite is visible) ----
                float2 uv      = spriteUV - 0.5;
                float2 worldUV = (IN.worldPos - IN.worldData.xy) / max(IN.worldData.z, 0.0001);

                float  dmg    = _DamageProgress;
                float  dmgVis = pow(dmg, _DamageCurve);

                fixed3 ashColor   = tex2D(_AshGradient,   float2(dmgVis, 0.5)).rgb;
                fixed3 crackColor = tex2D(_CrackGradient, float2(dmgVis, 0.5)).rgb;

                // ---- Base: black rubber, bleaches toward ash gradient under stress ----
                fixed3 col = lerp(fixed3(0.04, 0.04, 0.05), ashColor, dmgVis * dmgVis);

                // ---- Sphere projection ----
                float2 p     = uv * 2.0;
                float  rFlat = min(length(p), 0.9999);
                float  zSph  = sqrt(1.0 - rFlat * rFlat);
                float  phi   = atan2(p.y, p.x);
                float  theta = acos(zSph);
                float3 sphereNormal = float3(p.x, p.y, zSph);

                // Stable Voronoi UV — uses base warp/scale only so cells never move.
                // Damage boosts are applied to crack rendering, not cell positions.
                float  thetaNorm = theta / (UNITY_PI * 0.5);
                float  vorR = pow(thetaNorm, _SphereWarp) * _VoronoiScale;
                float2 vUV  = float2(cos(phi) * vorR, sin(phi) * vorR) + _VoronoiSeed;

                // ---- Leather-like surface grain (sampled in sphere-projected UV) ----
                // Degenerate guard: falls back to the canonical direction if
                // SceneLightService hasn't pushed yet (protects edit-time before its
                // first OnEnable/LateUpdate/OnValidate).
                float2 lightDirToward = dot(_SceneLightDir.xy, _SceneLightDir.xy) < 1e-4
                    ? float2(-0.707, 0.707)
                    : _SceneLightDir.xy;
                // LeatherGrain wants the light-travel (FROM-light) direction, but the
                // global points TOWARD the light — negate to convert.
                float grain = LeatherGrain(vUV, sphereNormal, _GrainScale, -lightDirToward);
                col += col * grain * _GrainStrength;

                // ---- Rim / subsurface fringe (world-space — never rotates) -----
                float rimW = _RimWidth * (1.0 - dmgVis * 0.72);
                float rimR = length(worldUV);
                float rim  = smoothstep(0.50 - rimW, 0.50, rimR) * alpha * (1.0 - dmgVis * 0.45);
                col        = lerp(col, _RimColor.rgb, rim);

                // ---- Voronoi stress cracks (using stable sphere-projected UV) ----
                float2 edgeDir;
                float2 voro = VoronoiWithDir(vUV, edgeDir);
                float  edge = voro.y - voro.x;

                // Damage boosts widen cracks via threshold and softness, not UV
                float warpBoost  = _SphereWarpDamageBoost * (dmgVis * dmgVis);
                float scaleBoost = _VoronoiScaleDamageBoost * (dmgVis * dmgVis);
                float dynThreshold = lerp(0.18, _CrackThreshold, dmgVis)
                                   + (warpBoost + scaleBoost) * 0.01;
                float baseSoftness = lerp(0.003, 0.018, dmgVis)
                                   + warpBoost * 0.005;
                // Widen the transition where UV changes rapidly to prevent aliasing
                float softness     = max(baseSoftness, fwidth(edge) * 1.5);
                float crackLine    = smoothstep(dynThreshold - softness, dynThreshold + softness, edge);
                crackLine          = pow(crackLine, lerp(1.0, _CrackSharpness * 0.03, dmgVis));

                // ---- Rubber fibers bridging across cracks ----
                float crackWidth = saturate(1.0 - edge / max(dynThreshold * 2.0, 0.001));
                float fibers     = RubberFibers(vUV, edgeDir,
                                     _FiberDensity, _FiberThickness, _VoronoiSeed.x, crackWidth);

                // Mask: fibers visible only inside cracks, strongest at mid-depth
                float crackMask = (1.0 - crackLine) * dmgVis;
                fibers *= crackMask * _FiberIntensity;

                fixed3 fiberColor = _FiberColor.rgb;

                float crackFade    = dmgVis;

                float cellFracture = (1.0 - crackLine) * dmgVis * 0.35;
                col = lerp(col, col * (1.0 - cellFracture), dmgVis);
                col = lerp(col, crackColor, crackLine * crackFade * (1.0 - rim));

                // Overlay fibers on top of crack color
                col = lerp(col, fiberColor, fibers * (1.0 - rim));

                // ---- Composite shadow under balloon (premultiplied alpha) ----
                // Blend mode is One / OneMinusSrcAlpha, so output is premultiplied.
                // Porter-Duff "sprite over shadow":
                //   RGB_out = RGB_sprite * A_sprite + RGB_shadow * A_shadow * (1 - A_sprite)
                //   A_out   = A_sprite + A_shadow * (1 - A_sprite)
                fixed3 premulSprite = col * alpha;
                fixed3 premulShadow = shadowRGB * shadowAlpha * (1.0 - alpha);

                return fixed4(premulSprite + premulShadow, combinedA);
            }
            ENDCG
        }
    }
}
