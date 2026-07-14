Shader "BalloonParty/Grid/PuffCloud"
{
    // ── Phase P1 — Cloud shader prototype ──────────────────────────────────
    // Procedural cloud on a SpriteRenderer quad (no assigned sprite).
    // Three octaves of a tileable baked noise texture (Tools > BalloonParty >
    // Generate Cloud Noise Texture), sampled in world space for spatial stability —
    // one fetch per octave replaced the original ALU simplex (~32% faster in-editor).
    // Slot-center array drives boundary falloff and occupancy masking — works
    // identically for single-slot (P1, 1 entry) and merged clusters (P3, N entries).
    //
    // Animation clock is shader-driven at runtime (_Time.y * _AnimationSpeed); C# only
    // feeds _TimeOffset in edit mode, where built-in _Time is frozen.
    //
    // _DisturbanceTex, _FieldBoundsMin, _FieldBoundsSize are GLOBAL shader
    // properties set by DisturbanceFieldService — NOT in the Properties block.
    //
    // Reference dimensions: SlotSeparation = (1.0, 0.85).
    // ───────────────────────────────────────────────────────────────────────
    Properties
    {
        _Color              ("Tint",               Color)              = (0.85, 0.95, 1.0, 1.0)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Noise)]
        // Renders the raw CloudNoise field as grayscale (ignores thresholds/lighting/density).
        [Toggle(_NOISE_DEBUG)] _NoiseDebug ("DEBUG: Visualize Noise Field", Float) = 0
        [NoScaleOffset] _NoiseTex ("Tileable Noise (R, linear)", 2D) = "gray" {}
        _NoisePeriod        ("Baked Noise Period",  Float)             = 8.0
        _NoiseScale         ("Global Noise Scale", Float)              = 1.0
        _BaseScale          ("Base Octave Scale",  Float)              = 2.0
        _DetailScale        ("Detail Octave Scale",Float)              = 5.0
        _FineScale          ("Fine Octave Scale",  Float)              = 10.0
        _ScrollSpeedBase    ("Scroll Speed Base",  Vector)             = (0.03, 0.02, 0, 0)
        _ScrollSpeedDetail  ("Scroll Speed Detail",Vector)             = (0.06, -0.04, 0, 0)
        _ScrollSpeedFine    ("Scroll Speed Fine",  Vector)             = (-0.04, 0.08, 0, 0)
        _EdgeLow            ("Edge Low Threshold", Range(0, 1))        = 0.35
        _EdgeHigh           ("Edge High Threshold",Range(0, 1))        = 0.55

        [Header(Visual)]
        _CloudColor         ("Cloud Color",        Color)              = (1, 1, 1, 0.6)
        _SpriteScale        ("Sprite Scale",       Range(0.3, 1.0))    = 0.70
        _BorderSoftness     ("Border Softness",    Range(0, 0.5))      = 0.15
        _SlotRadius         ("Slot Radius",        Float)              = 0.45
        _SlotBlend          ("Slot Union Smoothness", Range(0.01, 1))  = 0.35

        [Header(Lighting)]
        _LightColor         ("Highlight Color",    Color)              = (1, 1, 0.95, 1)
        _AmbientColor       ("Shadow Tint",        Color)              = (0.55, 0.58, 0.7, 1)
        _LightIntensity     ("Light Intensity",    Range(0, 1))        = 0.45
        // Diffuse response to the scene light (colour x intensity multiplies the whole cloud):
        // 0 = unlit (authored look always), 1 = fully lit.
        _LightInfluence     ("Light Influence",    Range(0, 1))        = 1
        // Domain-warps the LIGHT-FIELD lookup by the cloud's DETAIL-octave noise (world units), so a
        // local light's crisp stamp geometry (the laser cross, a bomb disc) gets a noisy, integrated
        // edge instead of reading as hard geometry. Kept small + high-frequency on purpose: the stamp
        // stays where it is (biased to the geometric shape) and only its boundary feathers into the
        // noise — a large/low-frequency warp instead snakes the whole beam into ribbons. A no-op where
        // no local light is near (the field is uniform there), so it never touches the rest look. 0 = off.
        _LightWarpAmount    ("Light Field Warp",   Range(0, 1.5))      = 0.35
        // Ignore local lights of this palette colour when reading the light field, so that colour of
        // light leaves the cloud untouched (e.g. the laser telegraph, tagged Unbreakable, shouldn't light
        // the clouds). [PaletteIndex] shows the named swatch dropdown; "None" stores -1 (ignore nothing).
        [PaletteIndex] _IgnoreLightPaletteIndex ("Ignore Light Colour", Float) = -1
        _NormalStrength     ("Normal Strength",    Range(0, 3))        = 1.2
        _NormalEpsilon      ("Normal Sample Offset",Range(0.001, 0.05))= 0.012

        [Header(Density)]
        [Toggle(_DENSITY_ON)] _EnableDensity ("Enable Density RT", Float) = 0
        _DisplaceWorldScale ("Displace World Scale", Range(0, 2))      = 0.5

        [Header(Animation)]
        _AnimationSpeed     ("Animation Speed",    Float)              = 0.8
        _TimeOffset         ("Time Offset",        Float)              = 0.0

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow",   Float)  = 0
        _ShadowColor        ("Shadow Color",       Color)              = (0.06, 0.06, 0.14, 0.35)
        _ShadowDistance     ("Shadow Distance",    Range(0, 0.4))      = 0.212
        _ShadowSoftness     ("Shadow Softness",    Range(0, 0.10))     = 0.03
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature _SHADOW_ON
            #pragma shader_feature _DENSITY_ON
            #pragma shader_feature _NOISE_DEBUG
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            // Max slot centers for merged clusters (P3); P1 uses 1.
            #define MAX_SLOTS 16

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

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;
            sampler2D _NoiseTex;
            float  _NoisePeriod;
            float  _NoiseScale;
            float  _BaseScale;
            float  _DetailScale;
            float  _FineScale;
            float4 _ScrollSpeedBase;
            float4 _ScrollSpeedDetail;
            float4 _ScrollSpeedFine;
            float  _EdgeLow;
            float  _EdgeHigh;
            fixed4 _CloudColor;
            float  _SpriteScale;
            float  _BorderSoftness;
            float  _SlotRadius;
            float  _SlotBlend;
            float  _TimeOffset;
            float  _AnimationSpeed;

            fixed4 _LightColor;
            fixed4 _AmbientColor;
            float  _LightIntensity;
            float  _LightInfluence;
            float  _LightWarpAmount;
            float  _IgnoreLightPaletteIndex;
            float  _NormalStrength;
            float  _NormalEpsilon;

            // Global shader properties — set once by DisturbanceFieldService,
            // not declared in Properties block so material defaults don't mask them.
            #ifdef _DENSITY_ON
            sampler2D _DisturbanceTex;
            float2    _FieldBoundsMin;
            float2    _FieldBoundsSize;
            float     _DisplaceWorldScale;
            #endif

            // Slot center positions in world space — set via MaterialPropertyBlock.
            float4 _SlotCentersWorld[MAX_SLOTS];
            // Same centers relative to the quad's own origin (bounds center), so the occupancy
            // mask can be evaluated in object space and the cloud follows this transform (and any
            // parent, e.g. the level-transition scenario root sliding down). Set alongside the
            // world array by ClusterView.
            float4 _SlotCentersLocal[MAX_SLOTS];
            // The quad's world origin at rest (bounds center with no Ascent lift). Lets the shader
            // reconstruct each fragment's rest-frame world position so noise + disturbance sample
            // where the field is valid even while the cloud is lifted mid-slide. Equals the live
            // object origin at rest, so it's a no-op then.
            float2 _RestOrigin;
            int    _SlotCount;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowDistance;
            float  _ShadowSoftness;
            #endif

            // One octave in [-1, 1]. The baked texture stores value in R over one tile of
            // _NoisePeriod noise units — repeat wrap makes p/_NoisePeriod seamless. The tile
            // is histogram-matched to the simplex the cloud thresholds were tuned against.
            float NoiseOctave(float2 p)
            {
                return tex2D(_NoiseTex, p / max(_NoisePeriod, 0.0001)).r * 2.0 - 1.0;
            }

            // Three-octave noise blend.
            // Weights: base 0.50, detail 0.30, fine 0.20.
            // Returns [0, 1] (remapped from raw [-1, 1] per octave).
            // Sampled continuously at world position: one unbroken field across the whole
            // board, so clusters can never seam against each other. Per-location uniqueness
            // comes from position itself, temporal variety from the scroll terms — the
            // per-cluster seed offsets this replaces cut the field at cluster borders.
            float CloudNoise(float2 wp, float t, out float lowFrequency)
            {
                float2 pBase   = wp * _BaseScale   * _NoiseScale + _ScrollSpeedBase.xy   * t;
                float2 pDetail = wp * _DetailScale  * _NoiseScale + _ScrollSpeedDetail.xy * t;
                float2 pFine   = wp * _FineScale    * _NoiseScale + _ScrollSpeedFine.xy   * t;

                float nLow  = NoiseOctave(pBase)   * 0.50;
                nLow       += NoiseOctave(pDetail) * 0.30;
                float n     = nLow + NoiseOctave(pFine) * 0.20;

                // Base+detail partial, renormalized to [0,1] — the lighting normal derives from
                // this via screen-space derivatives, where the fine octave would only sparkle.
                lowFrequency = nLow / 0.8 * 0.5 + 0.5;
                return n * 0.5 + 0.5;
            }

            // Two-octave variant for the shadow: its soft threshold blurs away the fine octave
            // anyway, so skip it. Renormalized to the same [0,1] range.
            float CloudNoiseSoft(float2 wp, float t)
            {
                float2 pBase   = wp * _BaseScale   * _NoiseScale + _ScrollSpeedBase.xy   * t;
                float2 pDetail = wp * _DetailScale  * _NoiseScale + _ScrollSpeedDetail.xy * t;

                float n  = NoiseOctave(pBase)   * 0.50;
                n       += NoiseOctave(pDetail) * 0.30;

                return n / 0.8 * 0.5 + 0.5;
            }

            // Occupancy falloff (0 far from any slot, 1 at/near a slot center) via a
            // smooth-min union of the per-slot distances. A hard min() creases the field
            // along the midlines between slot centers (their Voronoi edges), and the fade
            // band dips there — visible as a hexagonal web through solid cloud. The
            // polynomial smooth-min rounds the union so the area between neighbouring
            // slots stays filled; the outer silhouette is unaffected.
            float SlotFalloff(float2 wp)
            {
                float k = max(_SlotBlend, 0.0001);
                float invK = 1.0 / k;
                float minDist = 999.0;
                for (int i = 0; i < _SlotCount; i++)
                {
                    float d = length(wp - _SlotCentersLocal[i].xy);
                    float h = saturate(0.5 + 0.5 * (minDist - d) * invK);
                    minDist = lerp(minDist, d, h) - k * h * (1.0 - h);
                }

                return smoothstep(_SlotRadius + _BorderSoftness, _SlotRadius, minDist);
            }

            // Derive a pseudo-normal from the low-frequency noise gradient and apply
            // half-Lambert directional lighting. The gradient comes from screen-space
            // derivatives of a value the density pass already computed — the GPU provides
            // ddx/ddy nearly free, replacing four extra noise evaluations per pixel. The
            // 2·epsilon factor keeps the authored _NormalEpsilon/_NormalStrength scaling of
            // the central differences this replaces.
            fixed3 CloudLighting(float2 worldGradient, float2 worldPos)
            {
                // Clamp kills rare derivative spikes (2×2 quads straddling divergent state) —
                // a legit cloud slope never exceeds this.
                float dX = clamp(worldGradient.x * 2.0 * _NormalEpsilon * _NormalStrength, -1.5, 1.5);
                float dY = clamp(worldGradient.y * 2.0 * _NormalEpsilon * _NormalStrength, -1.5, 1.5);

                float3 normal = normalize(float3(-dX, -dY, 1.0));

                // Field-aware: sampled at the cloud's own world position (per-fragment),
                // falling back to the flat global when the field is off. Already points
                // toward the light — direct consumption, no sign flip.
                // A/B: the previous authored value (1,-1) lit clouds from lower-right
                // (opposite the global's upper-left). If that inverted look is the
                // intended art, negate here (ld = -SceneLightDirectionAt(worldPos)) —
                // decide in-editor against the cloud drop shadow.
                // Opt a specific stamp colour out of the cloud's lighting: where the local light here is
                // tagged with the ignored palette index, fall back to the flat ambient globals — the
                // cloud lights as if that light weren't there (the laser beam, tagged Unbreakable, is the
                // motivating case). A no-op when the index is -1 or the texel carries a different light.
                float ignoredIdx = _IgnoreLightPaletteIndex;
                bool ignoreLocal = ignoredIdx >= 0.0
                    && abs(SceneLightPaletteIndexAt(worldPos) - ignoredIdx) < 0.5;

                float2 ld = ignoreLocal ? SceneLightDirection() : SceneLightDirectionAt(worldPos);
                float3 lightVec = normalize(float3(ld, 0.6));

                float NdotL = dot(normal, lightVec);
                float halfLambert = NdotL * 0.5 + 0.5;

                // Authored shading first (Light Intensity is the CONTRAST of the ambient/highlight
                // modulation — not brightness), then the scene light's diffuse term multiplies the
                // whole cloud: colour AND intensity, eased by _LightInfluence. Coupling intensity
                // into the contrast was wrong — at zero light it flattened to bright white instead
                // of going dark.
                fixed3 lit = lerp(_AmbientColor.rgb, _LightColor.rgb, halfLambert);
                fixed3 shading = lerp(fixed3(1, 1, 1), lit, _LightIntensity);

                float3 sceneTint = ignoreLocal ? SceneLightTint() : SceneLightTintAt(worldPos);
                return shading * lerp(float3(1.0, 1.0, 1.0), sceneTint, _LightInfluence);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;

                float4 worldVert = mul(unity_ObjectToWorld, IN.vertex);
                OUT.worldPos = worldVert.xy;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {

                float2 wpOrig = IN.worldPos;
                // Occupancy mask is evaluated in object space so the cloud shape follows this
                // transform (and its parent). At rest the object origin equals the bounds center,
                // so wpLocal/_SlotCentersLocal reproduce the old world math byte-for-byte; only a
                // displaced quad (the Ascent slide) moves the silhouette.
                float2 objOrigin = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
                float2 wpLocal = wpOrig - objOrigin;
                // Rest-frame world position: fragment re-anchored at the rest origin, so noise and the
                // disturbance field are sampled as if the cloud were unlifted — the whole cloud reads as
                // one rigid object during the slide, and the field UV never leaves its valid bounds
                // (which flickered). wpRest == wpOrig at rest, so normal play is byte-identical.
                float2 wpRest = wpLocal + _RestOrigin;
                // Runtime clock is shader-driven via built-in _Time (no per-frame push);
                // _TimeOffset is only fed by C# in edit mode, where _Time is frozen.
                float  t  = _Time.y * _AnimationSpeed + _TimeOffset;

                // Boundary falloff — occupancy mask via slot centers (object space)
                float borderFade = SlotFalloff(wpLocal);

                #ifdef _SHADOW_ON
                // The shadow lands down-light of the cloud: direction derived from the scene
                // light (-toward-light) sampled at the cloud's OWN position (what casts the
                // shadow, not where it lands), only the distance stays authored — so rotating
                // the light moves the drop shadow together with the diffuse shading.
                float2 shadowOffset = -SceneLightDirectionAt(wpRest) * _ShadowDistance;
                float2 shadowWpWorld = wpRest   - shadowOffset;
                float2 shadowWpLocal = wpLocal  - shadowOffset;
                float  shadowFade = SlotFalloff(shadowWpLocal);
                #endif

                // The controller stretches ONE quad over the bounding box of every cluster
                // on the board, so with clusters far apart most fragments are nowhere near a
                // slot — bail before the texture fetches, not at the end of the shader.
                // Fragments discarded here keep executing as helper invocations, so the
                // ddx/ddy below stay defined for surviving quad neighbours (and those sit at
                // borderFade ≈ 0, where alpha hides any residual error anyway). The debug
                // variant skips the early-out — it wants the raw field everywhere.
                #ifndef _NOISE_DEBUG
                #ifdef _SHADOW_ON
                if (borderFade < 0.001 && shadowFade < 0.001)
                {
                    discard;
                }
                #else
                if (borderFade < 0.001)
                {
                    discard;
                }
                #endif
                #endif

                // Density field + displacement (P2+)
                #ifdef _DENSITY_ON
                float2 fieldUV = (wpRest - _FieldBoundsMin) / _FieldBoundsSize;
                float3 field = tex2D(_DisturbanceTex, fieldUV).rgb;
                // R is signed density (0.5 rest): a repulsion bump (> 0.5) thins the cloud; remap so
                // rest reads full and attraction (< 0.5) leaves it full.
                float density = saturate((1.0 - field.r) * 2.0);
                float2 displace = (field.gb - 0.5) * 2.0 * _DisplaceWorldScale;
                float displaceLen = length(displace);
                // How disturbed this texel is — 1.0 at full displacement, 0.0 at rest
                float disturbance = saturate(displaceLen / (_DisplaceWorldScale * 0.5 + 0.001));
                #endif

                float lowOrig;
                float noiseOrig = CloudNoise(wpRest, t, lowOrig);

                #ifdef _NOISE_DEBUG
                return fixed4(noiseOrig.xxx, 1.0);
                #endif

                // Lighting gradient — computed before the early returns and the disturbance
                // branch, where screen-space derivatives would be undefined. (The early
                // discard above is fine: discarded fragments continue as helper invocations.)
                // Pixel world size assumes the camera never rotates (it doesn't).
                float2 pixelWorld = max(float2(abs(ddx(wpOrig.x)), abs(ddy(wpOrig.y))), 1e-5);
                float2 lightGradient = float2(ddx(lowOrig), ddy(lowOrig)) / pixelWorld;

                // Noise-based cloud shape — crossfade between displaced and
                // undisturbed noise so reformation reveals fresh noise instead
                // of rubber-banding stretched noise back to rest. The displaced
                // evaluation only runs where the field is actually disturbed —
                // almost everywhere, almost always, it isn't.
                #ifdef _DENSITY_ON
                float cloudOrig = smoothstep(_EdgeLow, _EdgeHigh, noiseOrig);
                float cloud = cloudOrig;
                if (disturbance > 0.001)
                {
                    float2 wpDisp = wpRest + displace;
                    float lowDisp;
                    float noiseDisp = CloudNoise(wpDisp, t, lowDisp);
                    float cloudDisp = smoothstep(_EdgeLow, _EdgeHigh, noiseDisp);
                    cloud = lerp(cloudOrig, cloudDisp, disturbance);
                }
                cloud *= density;
                #else
                float cloud = smoothstep(_EdgeLow, _EdgeHigh, noiseOrig);
                #endif

                cloud *= borderFade;

                #ifdef _SHADOW_ON
                float mainAlpha = cloud * _CloudColor.a * IN.color.a;

                // The shadow composites BEHIND the cloud — its contribution is scaled by
                // (1 - mainAlpha) below — so under opaque cloud interior it is invisible and
                // its two noise fetches are pure waste. Only evaluate where the shadow's own
                // falloff reaches and the cloud doesn't fully cover it. Shadow-only pixels
                // (outside the cloud) still get here with mainAlpha 0 and survive the final
                // discard.
                float shadowAlpha = 0.0;
                if (shadowFade > 0.001 && mainAlpha < 0.999)
                {
                    float shadowNoise = CloudNoiseSoft(shadowWpWorld, t);
                    float shadowCloud = smoothstep(_EdgeLow, _EdgeHigh, shadowNoise);
                    #ifdef _DENSITY_ON
                    shadowCloud *= density;
                    #endif
                    shadowCloud *= shadowFade;

                    shadowAlpha = shadowCloud * _ShadowColor.a * IN.color.a;
                    shadowAlpha *= smoothstep(0.0, _ShadowSoftness + 0.01, shadowCloud);
                    shadowAlpha *= ShadowLightFadeAt(wpRest);
                }

                if (cloud < 0.001 && shadowAlpha < 0.001) discard;

                // Pure shadow pixel — main cloud absent
                if (cloud < 0.001)
                {
                    return fixed4(_ShadowColor.rgb, shadowAlpha);
                }

                // Domain-warp the light-field lookup by the cloud's DETAIL-octave noise (two
                // decorrelated taps, animated with the cloud so the warp scrolls with it): a local
                // light's crisp stamp edge feathers into the same turbulence that shapes the cloud.
                // Detail (not base) octave + a small amplitude keeps the warp high-frequency, so the
                // stamp holds its geometric shape and only its boundary breaks up — a coarse/large warp
                // snakes the whole beam into ribbons. Uniform-field regions (no local light) warp to an
                // identical value, so the rest look is untouched.
                float2 warpP = wpRest * _DetailScale * _NoiseScale + _ScrollSpeedDetail.xy * t;
                float2 wpLight = wpRest + float2(
                    NoiseOctave(warpP + float2(31.7, 12.3)),
                    NoiseOctave(warpP + float2(-8.4, 47.1))) * _LightWarpAmount;

                // Compose main cloud with shadow behind
                fixed3 lighting = CloudLighting(lightGradient, wpLight);
                fixed3 mainRgb  = _CloudColor.rgb * IN.color.rgb * lighting;

                fixed  combinedA   = mainAlpha + shadowAlpha * (1.0 - mainAlpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (mainRgb * mainAlpha + _ShadowColor.rgb * shadowAlpha * (1.0 - mainAlpha)) / combinedA
                    : mainRgb;

                return fixed4(combinedRGB, combinedA);
                #else
                if (cloud < 0.001) discard;

                float mainAlpha = cloud * _CloudColor.a * IN.color.a;
                fixed3 lighting = CloudLighting(lightGradient, wpRest);
                fixed3 mainRgb  = _CloudColor.rgb * IN.color.rgb * lighting;

                return fixed4(mainRgb, mainAlpha);
                #endif
            }
            ENDCG
        }
    }
}

