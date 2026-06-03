Shader "BalloonParty/Grid/Bush"
{
    // Procedural top-down cartoony bush on a SpriteRenderer quad.
    // SDF-based shape with smooth-minimum merging across slot centers.
    // Multi-octave Simplex noise for organic leaf detail + edge bumps.
    //
    // GPU instancing DISABLED — per-instance _TimeOffset driven via MaterialPropertyBlock.
    //
    // _DisturbanceTex, _FieldBoundsMin, _FieldBoundsSize are GLOBAL shader
    // properties set by DisturbanceFieldService — NOT in the Properties block.
    //
    // MPB contract: _SlotCentersWorld, _SlotCount, _TimeOffset — set by ClusterView.
    Properties
    {
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shape)]
        _SlotRadius         ("Slot Radius",          Float)              = 0.40
        _RadiusJitter       ("Radius Jitter",        Range(0, 0.15))     = 0.06
        _EdgeNoiseFreq      ("Edge Noise Frequency", Float)              = 4.0
        _EdgeNoiseAmount    ("Edge Noise Amount",    Range(0, 0.2))      = 0.08
        _SminK              ("Smooth Min K",         Range(0.05, 0.5))   = 0.20
        _AAWidth            ("AA Edge Width",        Range(0.001, 0.03)) = 0.008

        [Header(Surface)]
        _BaseColor          ("Base Color",           Color)              = (0.18, 0.45, 0.12, 1.0)
        _LeafVariationColor ("Leaf Variation Color", Color)              = (0.25, 0.55, 0.15, 1.0)
        _LeafNoiseFreq      ("Leaf Noise Frequency", Float)              = 6.0

        [Header(Lighting)]
        [Toggle(_LIGHTING_ON)] _EnableLighting ("Enable Lighting", Float) = 1
        _LightDir           ("Light Direction",      Vector)             = (-0.4, 0.7, 0, 0)
        _LightColor         ("Highlight Color",      Color)              = (1, 1, 0.9, 1)
        _AmbientColor       ("Shadow Tint",          Color)              = (0.08, 0.22, 0.05, 1)
        _LightIntensity     ("Light Intensity",      Range(0, 1))        = 0.50
        _NormalStrength     ("Normal Strength",       Range(0, 3))        = 1.5
        _NormalEpsilon      ("Normal Sample Offset",  Range(0.001, 0.05))= 0.015

        [Header(Rim)]
        _RimWidth           ("Rim Width",            Range(0, 0.15))     = 0.04
        _RimIntensity       ("Rim Intensity",        Range(0, 1))        = 0.35

        [Header(Wind)]
        _WindSpeed          ("Wind Speed",           Range(0, 2))        = 0.4
        _WindAmount         ("Wind Amount",          Range(0, 0.1))      = 0.02

        [Header(Animation)]
        _TimeOffset         ("Time Offset",          Float)              = 0.0

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow", Float)      = 1
        _ShadowColor        ("Shadow Color",         Color)              = (0.04, 0.04, 0.08, 0.45)
        _ShadowOffsetX      ("Shadow Offset X",     Range(-0.15, 0.15)) = 0.03
        _ShadowOffsetY      ("Shadow Offset Y",     Range(-0.15, 0.15)) = -0.04
        _ShadowSoftness     ("Shadow Softness",      Range(0, 0.10))     = 0.04

        [Header(Center Shadow)]
        [Toggle(_CENTER_SHADOW_ON)] _EnableCenterShadow ("Enable Center Shadow", Float) = 0
        _CenterShadowDarkness ("Center Shadow Darkness", Range(0.3, 1.0)) = 0.75

        [Header(Disturbance)]
        [Toggle(_DISTURBANCE_ON)] _EnableDisturbance ("Enable Disturbance", Float) = 0
        _DisplaceWorldScale  ("Displace World Scale",  Range(0, 2))      = 0.3
        _EdgeDisturbanceScale("Edge Disturbance Scale", Range(0, 3))     = 1.5
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "TransparentCutout"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature _SHADOW_ON
            #pragma shader_feature _CENTER_SHADOW_ON
            #pragma shader_feature _DISTURBANCE_ON
            #pragma shader_feature _LIGHTING_ON
            #include "UnityCG.cginc"
            #include "Assets/Shaders/BalloonParty/Noise/SimplexNoise2D.cginc"

            #define MAX_SLOTS 8

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            // ── Instancing boilerplate (SpriteRenderer color tint) ──
            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            // ── Uniform declarations ──
            float  _SlotRadius;
            float  _RadiusJitter;
            float  _EdgeNoiseFreq;
            float  _EdgeNoiseAmount;
            float  _SminK;
            float  _AAWidth;

            fixed4 _BaseColor;
            fixed4 _LeafVariationColor;
            float  _LeafNoiseFreq;

            float4 _LightDir;
            fixed4 _LightColor;
            fixed4 _AmbientColor;
            float  _LightIntensity;
            float  _NormalStrength;
            float  _NormalEpsilon;

            float  _RimWidth;
            float  _RimIntensity;

            float  _WindSpeed;
            float  _WindAmount;
            float  _TimeOffset;

            // MPB contract — set by ClusterView base class
            float4 _SlotCentersWorld[MAX_SLOTS];
            int    _SlotCount;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowOffsetX;
            float  _ShadowOffsetY;
            float  _ShadowSoftness;
            #endif

            #ifdef _CENTER_SHADOW_ON
            float _CenterShadowDarkness;
            #endif

            #ifdef _DISTURBANCE_ON
            sampler2D _DisturbanceTex;
            float2    _FieldBoundsMin;
            float2    _FieldBoundsSize;
            float     _DisplaceWorldScale;
            float     _EdgeDisturbanceScale;
            #endif

            // ── Helper: polynomial smooth-minimum ──
            float smin(float a, float b, float k)
            {
                float h = saturate(0.5 + 0.5 * (b - a) / k);
                return lerp(b, a, h) - k * h * (1.0 - h);
            }

            // ── 2.2: Per-slot circle SDF with smooth-minimum merging ──
            // Returns minimum SDF distance (negative = inside).
            // Outputs nearest slot's seed (.z) for per-slot variation.
            float BushSDF(float2 wp, out float slotSeed)
            {
                float d = 999.0;
                slotSeed = 0.0;
                float minRawDist = 999.0;

                for (int i = 0; i < _SlotCount; i++)
                {
                    float2 center = _SlotCentersWorld[i].xy;
                    float seed = _SlotCentersWorld[i].z;

                    float hash = frac(sin(dot(center, float2(127.1, 311.7))) * 43758.5453);
                    float radius = _SlotRadius + (hash - 0.5) * 2.0 * _RadiusJitter;

                    float dist = length(wp - center) - radius;
                    d = smin(d, dist, _SminK);

                    float rawDist = length(wp - center);
                    if (rawDist < minRawDist)
                    {
                        minRawDist = rawDist;
                        slotSeed = seed;
                    }
                }
                return d;
            }

            // ── 2.3: Edge noise (2-octave, per-slot seed offset) ──
            float EdgeNoise(float2 wp, float t, float slotSeed)
            {
                float2 seedOffset = float2(slotSeed * 61.7, slotSeed * 37.3);
                float2 p1 = (wp + seedOffset) * _EdgeNoiseFreq;
                float2 p2 = (wp + seedOffset) * _EdgeNoiseFreq * 2.17;

                float n  = SimplexNoise2D(p1 + float2(t * 0.1, 0.0)) * 0.65;
                n       += SimplexNoise2D(p2 + float2(0.0, t * 0.15)) * 0.35;

                return n;
            }

            // ── 2.10: Wind displacement (1-octave, low frequency) ──
            float2 WindDisplace(float2 wp, float t)
            {
                float2 windP = wp * 0.5 + float2(t * _WindSpeed, t * _WindSpeed * 0.7);
                float windN = SimplexNoise2D(windP);
                return float2(windN, windN * 0.6) * _WindAmount;
            }

            // ── 2.5: Leaf noise (3-octave, world-space) — colour modulation ──
            float LeafNoise(float2 wp, float t, float slotSeed)
            {
                float2 seedOff = float2(slotSeed * 91.3, slotSeed * 53.7);
                float2 p = (wp + seedOff) * _LeafNoiseFreq;

                float n  = SimplexNoise2D(p + float2(t * 0.02, t * 0.01)) * 0.50;
                n       += SimplexNoise2D(p * 2.13 + float2(-t * 0.03, t * 0.02)) * 0.30;
                n       += SimplexNoise2D(p * 4.37 + float2(t * 0.01, -t * 0.04)) * 0.20;

                return n * 0.5 + 0.5;
            }

            // ── 2.6: Leaf noise lite (1-octave) — used for lighting gradient only ──
            float LeafNoiseLite(float2 wp, float t, float slotSeed)
            {
                float2 seedOff = float2(slotSeed * 91.3, slotSeed * 53.7);
                float2 p = (wp + seedOff) * _LeafNoiseFreq;
                return SimplexNoise2D(p + float2(t * 0.02, t * 0.01)) * 0.5 + 0.5;
            }

            // ── 2.6: Pseudo-lighting (half-Lambert from noise gradient) ──
            fixed3 BushLighting(float2 wp, float t, float slotSeed)
            {
                float eps = _NormalEpsilon;
                float nR = LeafNoiseLite(wp + float2( eps, 0), t, slotSeed);
                float nL = LeafNoiseLite(wp + float2(-eps, 0), t, slotSeed);
                float nU = LeafNoiseLite(wp + float2(0,  eps), t, slotSeed);
                float nD = LeafNoiseLite(wp + float2(0, -eps), t, slotSeed);

                float dX = (nR - nL) * _NormalStrength;
                float dY = (nU - nD) * _NormalStrength;
                float3 normal = normalize(float3(-dX, -dY, 1.0));

                float2 ld = normalize(_LightDir.xy);
                float3 lightVec = normalize(float3(ld, 0.6));
                float NdotL = dot(normal, lightVec);
                float halfLambert = NdotL * 0.5 + 0.5;

                fixed3 lit = lerp(_AmbientColor.rgb, _LightColor.rgb, halfLambert);
                return lerp(fixed3(1, 1, 1), lit, _LightIntensity);
            }

            // ── Vertex shader ──
            v2f vert(appdata_t IN)
            {
                v2f OUT;

                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _RendererColor;

                float4 worldVert = mul(unity_ObjectToWorld, IN.vertex);
                OUT.worldPos = worldVert.xy;

                return OUT;
            }

            // ── Fragment shader ──
            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;
                float  t  = _TimeOffset;

                // ── Step 1: Wind displacement ──
                float2 wpAnim = wp + WindDisplace(wp, t);

                // ── Step 2: SDF shape (uses static wp — shape stays anchored) ──
                float slotSeed;
                float d = BushSDF(wp, slotSeed);

                // ── Step 3: Edge noise (uses static wp — stable silhouette) ──
                float edgeN = EdgeNoise(wp, t, slotSeed);
                float edgeAmount = _EdgeNoiseAmount;

                // ── Step 4: Disturbance ──
                #ifdef _DISTURBANCE_ON
                float2 fieldUV = (wp - _FieldBoundsMin) / _FieldBoundsSize;
                float3 field = tex2D(_DisturbanceTex, fieldUV).rgb;
                float2 displace = (field.gb - 0.5) * 2.0 * _DisplaceWorldScale;
                float displaceLen = length(displace);
                float disturbance = saturate(displaceLen / (_DisplaceWorldScale * 0.5 + 0.001));

                // Warp leaf noise sampling coords
                wpAnim += displace;

                // Amplify edge noise near disturbance
                edgeAmount *= (1.0 + disturbance * _EdgeDisturbanceScale);
                #endif

                d -= edgeN * edgeAmount;

                // ── Step 5: Alpha clip with AA ──
                float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, d);

                // ── Shadow computation (needs to happen before early discard) ──
                #ifdef _SHADOW_ON
                float2 shadowWp = wp - float2(_ShadowOffsetX, _ShadowOffsetY);
                float shadowSeed;
                float shadowD = BushSDF(shadowWp, shadowSeed);
                float shadowEdgeN = EdgeNoise(shadowWp, t, shadowSeed);
                float shadowEdgeAmount = _EdgeNoiseAmount;
                #ifdef _DISTURBANCE_ON
                shadowEdgeAmount *= (1.0 + disturbance * _EdgeDisturbanceScale);
                #endif
                shadowD -= shadowEdgeN * shadowEdgeAmount;
                float shadowAlpha = (1.0 - smoothstep(-_ShadowSoftness, 0.0, shadowD))
                                  * _ShadowColor.a * IN.color.a;

                if (alpha < 0.001 && shadowAlpha < 0.001) discard;

                // Shadow-only pixel — main body absent
                if (alpha < 0.001)
                {
                    return fixed4(_ShadowColor.rgb, shadowAlpha);
                }
                #else
                if (alpha < 0.001) discard;
                #endif

                // ── Step 6: Leaf colour modulation (expensive — after early discard) ──
                float leafN = LeafNoise(wpAnim, t, slotSeed);
                fixed3 surfaceColor = lerp(_BaseColor.rgb, _LeafVariationColor.rgb, leafN);

                // ── Step 7: Pseudo-lighting ──
                #ifdef _LIGHTING_ON
                fixed3 lighting = BushLighting(wpAnim, t, slotSeed);
                surfaceColor *= lighting;
                #endif

                // ── Step 8: Edge highlight rim ──
                float rim = smoothstep(_RimWidth, 0.0, abs(d));
                surfaceColor = lerp(surfaceColor, _LightColor.rgb, rim * _RimIntensity);

                // ── Step 9: Centre shadow ──
                #ifdef _CENTER_SHADOW_ON
                float centerDist = 999.0;
                for (int i = 0; i < _SlotCount; i++)
                {
                    centerDist = min(centerDist, length(wp - _SlotCentersWorld[i].xy));
                }
                float centerFade = 1.0 - smoothstep(_SlotRadius * 0.3, _SlotRadius * 0.8, centerDist);
                surfaceColor *= lerp(1.0, _CenterShadowDarkness, centerFade);
                #endif

                // ── Step 10: Final composition ──
                fixed3 mainRgb = surfaceColor * IN.color.rgb;
                float mainAlpha = alpha * IN.color.a;

                #ifdef _SHADOW_ON
                fixed  combinedA   = mainAlpha + shadowAlpha * (1.0 - mainAlpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (mainRgb * mainAlpha + _ShadowColor.rgb * shadowAlpha * (1.0 - mainAlpha)) / combinedA
                    : mainRgb;
                return fixed4(combinedRGB, combinedA);
                #else
                return fixed4(mainRgb, mainAlpha);
                #endif
            }
            ENDCG
        }
    }
}

