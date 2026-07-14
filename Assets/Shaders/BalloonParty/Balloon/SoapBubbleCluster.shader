Shader "BalloonParty/Balloon/SoapBubbleCluster"
{
    // ── Investigation prototype ───────────────────────────────────────────────
    // Renders a cluster of up to 5 discrete soap bubbles on a single quad.
    // Each bubble is an independent circle SDF with its own radius — they do
    // NOT merge (unlike metaballs).  The Voronoi region of each circle
    // determines ownership.
    //
    // Junction membrane (Plateau's Law, 2-D approximation):
    //   • junctionLine  — the Voronoi boundary where bestSdf ≈ secondSdf.
    //                     This is the true "wall" between bubbles; it runs
    //                     through the full interior depth of the cluster.
    //   • overlapZone   — pixels inside both bubbles simultaneously get a
    //                     slight opacity boost (two film surfaces, denser).
    //
    // _BubbleCount (1–5) shrinks the cluster via MaterialPropertyBlock.
    // Bubbles are removed highest-index first for coherent degradation.
    // ─────────────────────────────────────────────────────────────────────────
    Properties
    {
        _Color          ("Tint",           Color)              = (0.85, 0.95, 1.0, 1.0)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Cluster)]
        _SpriteScale    ("Sprite Scale",   Range(0.50, 1.00))  = 0.80
        _BubbleCount    ("Bubble Count",   Range(1, 5))        = 5
        _BubbleRadius   ("Bubble Radius",  Range(0.08, 0.30))  = 0.17
        _RadiusVariance ("Radius Variance",Range(0, 0.35))     = 0.18

        [Header(Film)]
        _FilmWidth      ("Film Width",     Range(0.005, 0.10)) = 0.040
        _FilmAlpha      ("Film Alpha",     Range(0, 1))        = 0.92
        _InteriorAlpha  ("Interior Alpha", Range(0, 0.40))     = 0.10

        [Header(Junction Membrane)]
        // The LINE is the Voronoi boundary through the cluster interior.
        // The OVERLAP is a faint fill wherever two circles intersect.
        _JunctionLineWidth   ("Line Width",        Range(0.002, 0.04)) = 0.012
        _JunctionLineAlpha   ("Line Alpha",        Range(0, 1))        = 0.70
        _JunctionOverlapAlpha("Overlap Area Alpha",Range(0, 0.30))     = 0.08

        [Header(Iridescence)]
        _IridescenceSat   ("Saturation",   Range(0, 1))        = 0.55
        _IridescenceAmt   ("Mix Amount",   Range(0, 1))        = 0.70
        _IridescenceSpeed ("Hue Speed",    Range(0, 0.20))     = 0.025
        _TimeOffset       ("Time Offset",  Float)              = 0.0
        _FloatSpeed       ("Float Speed",  Float)              = 0.0
        _RotationSpeed    ("Rotation Speed (rad per s)", Float) = 0.0

        [Header(Specular)]
        _SpecColor      ("Specular",       Color)              = (1, 1, 1, 1)
        _SpecSize       ("Size",           Range(0.01, 0.30))  = 0.10
        _SpecSharpness  ("Sharpness",      Range(2, 25))       = 6.0
        // Direction is derived from the scene light; only the distance stays authored.
        _SpecDistance   ("Distance",       Range(0, 0.35))     = 0.1414

        [Header(Rotation)]
        // Driven per-instance by SoapBubbleClusterVariant (radians).
        // Applying rotation here keeps the shadow offset axis-aligned regardless
        // of how much the cluster has rotated.
        _Rotation       ("Rotation (rad)",  Float)              = 0.0

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow    ("Enable Shadow",   Float)              = 0
        _ShadowColor    ("Shadow Color",   Color)              = (0.06, 0.06, 0.14, 0.50)
        _ShadowFilmWidth("Film Width",     Range(0.002, 0.03)) =  0.008
        _ShadowSeamWidth("Seam Width",     Range(0.001, 0.02)) =  0.004
        // Direction is derived from the scene light (-L); only the distance stays authored.
        _ShadowDistance ("Distance",       Range(0, 0.35))     =  0.0566
        _ShadowSoftness ("Softness",       Range(0, 0.06))     =  0.015
        // _FloatAmount stays here because it is a purely visual scale with no
        // time dependency; the clock itself is _Time.y * _FloatSpeed + _TimeOffset
        // (SoapBubbleClusterVariant pushes speed/phase once at Bind, and zeroes the
        // speeds in edit mode to drive the preview from editor time instead).
        _FloatAmount    ("Per-Bubble Amount", Range(0, 0.06))   = 0.025

        [Header(Breathe)]
        // Slow cluster-wide scale oscillation.  All bubble positions scale
        // from the cluster centre, so inter-bubble distances change and the
        // junction seams visibly shift back and forth.
        _BreatheAmount  ("Scale Amount",  Range(0, 0.15))       = 0.07
        _BreatheSpeed   ("Speed (rel)",   Range(0, 2))          = 0.38
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
            #pragma multi_compile_instancing
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
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
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
            float  _SpriteScale;
            float  _BubbleCount;
            float  _BubbleRadius;
            float  _RadiusVariance;
            float  _FilmWidth;
            float  _FilmAlpha;
            float  _InteriorAlpha;
            float  _JunctionLineWidth;
            float  _JunctionLineAlpha;
            float  _JunctionOverlapAlpha;
            float  _IridescenceSat;
            float  _IridescenceAmt;
            float  _IridescenceSpeed;
            float  _TimeOffset;
            float  _FloatSpeed;
            fixed4 _SpecColor;
            float  _SpecSize;
            float  _SpecSharpness;
            float  _SpecDistance;
            float  _FloatAmount;
            float  _BreatheAmount;
            float  _BreatheSpeed;
            float  _Rotation;
            float  _RotationSpeed;

            // Global shader property — set by SceneLightService, not in Properties so
            // material values can't mask it. Points TOWARD the light, normalized;
            // canonical (-0.707, 0.707) = upper-left.
            float4 _SceneLightDir;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowFilmWidth;
            float  _ShadowSeamWidth;
            float  _ShadowDistance;
            float  _ShadowSoftness;
            #endif

            // ── Per-count cluster layouts ──────────────────────────────────
            // Each layout is centred at (0,0) with adjacent-bubble spacing ~0.22.
            // Inactive slots are set to (0,0) — they get pushed to kFar via the
            // sdf inactive check so they never contribute to rendering.
            //
            // Removal always decrements _BubbleCount, switching to the next
            // smaller layout, which naturally collapses the shape coherently.

            // count = 1 — single centred bubble
            static const float2 kLayout1[5] =
            {
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
            };

            // count = 2 — horizontal pair
            static const float2 kLayout2[5] =
            {
                float2(-0.110,  0.000),
                float2( 0.110,  0.000),
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
            };

            // count = 3 — equilateral triangle (point up), spacing 0.22
            //   centroid at y ≈ 0 (base at y=-0.063, apex at y=+0.127)
            static const float2 kLayout3[5] =
            {
                float2(-0.110, -0.063),
                float2( 0.110, -0.063),
                float2( 0.000,  0.127),
                float2( 0.000,  0.000),
                float2( 0.000,  0.000),
            };

            // count = 4 — square, spacing 0.22
            static const float2 kLayout4[5] =
            {
                float2(-0.110, -0.110),
                float2( 0.110, -0.110),
                float2(-0.110,  0.110),
                float2( 0.110,  0.110),
                float2( 0.000,  0.000),
            };

            // count = 5 — regular pentagon (point up)
            //   circumradius ≈ 0.187, adjacent spacing ≈ 0.22
            static const float2 kLayout5[5] =
            {
                float2( 0.000,  0.187),
                float2( 0.178,  0.058),
                float2( 0.110, -0.151),
                float2(-0.110, -0.151),
                float2(-0.178,  0.058),
            };

            // Returns the canonical rest position for bubble i at a given count.
            float2 ClusterCentre(int i, int count)
            {
                if (count <= 1) return kLayout1[i];
                if (count == 2) return kLayout2[i];
                if (count == 3) return kLayout3[i];
                if (count == 4) return kLayout4[i];
                return kLayout5[i];
            }

            // Per-bubble radius offset in [-1, 1].
            // Final radius = _BubbleRadius * (1 + _RadiusVariance * kRadiusVar[i]).
            // Values chosen so no single bubble dominates visually.
            static const float kRadiusVar[5] =
            {
                 0.00,   // 0 — centre bubble, base size
                -0.15,   // 1 — slightly smaller
                 0.14,   // 2 — slightly larger
                -0.22,   // 3 — noticeably smaller (top-right)
                 0.20,   // 4 — noticeably larger  (top-left)
            };

            // Compact HSV → RGB (branch-free)
            fixed3 HsvToRgb(float h, float s, float v)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(h + K.xyz) * 6.0 - K.www);
                return v * lerp(K.xxx, saturate(p - K.xxx), s);
            }

            // Guarded read of the scene light (see SceneLightService): normalized, toward
            // the light; falls back to the canonical direction if the global hasn't been
            // pushed yet (protects edit-time before its first OnEnable/LateUpdate/OnValidate).
            float2 SceneLightDirection()
            {
                return dot(_SceneLightDir.xy, _SceneLightDir.xy) < 1e-4
                    ? float2(-0.707, 0.707)
                    : _SceneLightDir.xy;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 uvRaw = (IN.texcoord - 0.5) / _SpriteScale;
                float  t     = _Time.y * _FloatSpeed + _TimeOffset;
                int    cnt   = (int)round(_BubbleCount);

                // ── Rotation applied before everything else ────────────────
                // Rotating uvRaw here instead of the transform keeps the shadow
                // offset direction fixed in quad space regardless of cluster angle.
                // The shadow offset is applied to uvRaw BEFORE rotating so it
                // stays in the unrotated (world-aligned) frame; the same rotation
                // is then applied to bring it into the cluster's own frame.
                float rotationAngle = _Rotation + _Time.y * _RotationSpeed;
                float cosR = cos(rotationAngle);
                float sinR = sin(rotationAngle);
                float2 uv  = float2(uvRaw.x * cosR - uvRaw.y * sinR,
                                    uvRaw.x * sinR + uvRaw.y * cosR);

                // ── Per-bubble radii (variance applied from constants) ──────
                float r0 = _BubbleRadius * (1.0 + _RadiusVariance * kRadiusVar[0]);
                float r1 = _BubbleRadius * (1.0 + _RadiusVariance * kRadiusVar[1]);
                float r2 = _BubbleRadius * (1.0 + _RadiusVariance * kRadiusVar[2]);
                float r3 = _BubbleRadius * (1.0 + _RadiusVariance * kRadiusVar[3]);
                float r4 = _BubbleRadius * (1.0 + _RadiusVariance * kRadiusVar[4]);

                // ── Cluster breathe ────────────────────────────────────────
                float breathe = 1.0 + _BreatheAmount * sin(t * _BreatheSpeed);

                // ── Bubble centres with independent micro-float ────────────
                float2 c0 = ClusterCentre(0, cnt) * breathe + float2(sin(t),        cos(t * 0.71))       * _FloatAmount;
                float2 c1 = ClusterCentre(1, cnt) * breathe + float2(sin(t + 1.1),  cos(t * 0.83 + 2.3)) * _FloatAmount;
                float2 c2 = ClusterCentre(2, cnt) * breathe + float2(sin(t + 2.2),  cos(t * 0.61 + 1.0)) * _FloatAmount;
                float2 c3 = ClusterCentre(3, cnt) * breathe + float2(sin(t + 3.3),  cos(t * 0.92 + 3.7)) * _FloatAmount;
                float2 c4 = ClusterCentre(4, cnt) * breathe + float2(sin(t + 4.4),  cos(t * 0.77 + 0.5)) * _FloatAmount;

                const float kFar = -9999.0;
                const float kEps = 0.0002;

                float sdf0 = (cnt >= 1) ? (r0 - length(uv - c0)) : kFar;
                float sdf1 = (cnt >= 2) ? (r1 - length(uv - c1)) : kFar;
                float sdf2 = (cnt >= 3) ? (r2 - length(uv - c2)) : kFar;
                float sdf3 = (cnt >= 4) ? (r3 - length(uv - c3)) : kFar;
                float sdf4 = (cnt >= 5) ? (r4 - length(uv - c4)) : kFar;

                float bestSdf = max(max(max(max(sdf0, sdf1), sdf2), sdf3), sdf4);

                // ── Shadow (rim + seams only, no interior fill) ────────────
                // Computed before the main early-discard so shadow-only pixels
                // (outside the main cluster footprint) are not dropped.
                #ifdef _SHADOW_ON
                // Shadow offset applied in UNROTATED space so the shadow direction
                // is always fixed relative to the quad axes, independent of _Rotation.
                // We then apply the same rotation to sample the cluster SDFs correctly.
                // Direction is derived from the scene light (-L); only the distance
                // stays authored — rotating the light moves the shadow together with
                // every other light-derived effect.
                float2 shadowOffset = -SceneLightDirection() * _ShadowDistance;
                float2 sRaw = uvRaw - shadowOffset;
                float2 sUV  = float2(sRaw.x * cosR - sRaw.y * sinR,
                                     sRaw.x * sinR + sRaw.y * cosR);
                float sh0 = (cnt >= 1) ? (r0 - length(sUV - c0)) : kFar;
                float sh1 = (cnt >= 2) ? (r1 - length(sUV - c1)) : kFar;
                float sh2 = (cnt >= 3) ? (r2 - length(sUV - c2)) : kFar;
                float sh3 = (cnt >= 4) ? (r3 - length(sUV - c3)) : kFar;
                float sh4 = (cnt >= 5) ? (r4 - length(sUV - c4)) : kFar;

                float shBest = max(max(max(max(sh0, sh1), sh2), sh3), sh4);

                // Shadow film: a band of fixed width _ShadowFilmWidth at the bubble surface.
                // Kept independent of _FilmWidth so the shadow rim can be much thinner
                // than the main rim.  _ShadowSoftness blurs both edges without
                // changing the total band thickness.
                float shadowFilm = smoothstep(-_ShadowSoftness, 0.0, shBest)
                                 * smoothstep(_ShadowFilmWidth, _ShadowFilmWidth - _ShadowSoftness, shBest);

                // Shadow seams: junction lines projected at the shadow offset.
                float shm0 = (sh0 < shBest - kEps) ? sh0 : kFar;
                float shm1 = (sh1 < shBest - kEps) ? sh1 : kFar;
                float shm2 = (sh2 < shBest - kEps) ? sh2 : kFar;
                float shm3 = (sh3 < shBest - kEps) ? sh3 : kFar;
                float shm4 = (sh4 < shBest - kEps) ? sh4 : kFar;
                float shSecond  = max(max(max(max(shm0, shm1), shm2), shm3), shm4);
                float shBorderGap  = shBest - shSecond;
                float shadowSeam   = smoothstep(_ShadowSeamWidth, 0.0, shBorderGap)
                                   * step(0.0, shSecond);

                float shadowAlpha = saturate(shadowFilm + shadowSeam)
                                  * _ShadowColor.a * IN.color.a;

                // Discard only when outside BOTH the main cluster and the shadow.
                if (bestSdf < 0.0 && shadowAlpha < 0.001) discard;

                // Pure shadow pixel — main cluster is absent here.
                if (bestSdf < 0.0)
                {
                    return fixed4(_ShadowColor.rgb, shadowAlpha);
                }
                #else
                if (bestSdf < 0.0) discard;
                #endif

                // ── Voronoi owner ──────────────────────────────────────────
                float2 ownCentre = c0;
                if (sdf1 > sdf0)                               ownCentre = c1;
                if (sdf2 > max(sdf0, sdf1))                    ownCentre = c2;
                if (sdf3 > max(max(sdf0, sdf1), sdf2))         ownCentre = c3;
                if (sdf4 > max(max(max(sdf0,sdf1),sdf2),sdf3)) ownCentre = c4;

                // ── Second-highest SDF ─────────────────────────────────────
                float m0 = (sdf0 < bestSdf - kEps) ? sdf0 : kFar;
                float m1 = (sdf1 < bestSdf - kEps) ? sdf1 : kFar;
                float m2 = (sdf2 < bestSdf - kEps) ? sdf2 : kFar;
                float m3 = (sdf3 < bestSdf - kEps) ? sdf3 : kFar;
                float m4 = (sdf4 < bestSdf - kEps) ? sdf4 : kFar;
                float secondSdf = max(max(max(max(m0, m1), m2), m3), m4);

                // ── Junction membrane ──────────────────────────────────────
                float borderGap    = bestSdf - secondSdf;
                float junctionLine = smoothstep(_JunctionLineWidth, 0.0, borderGap)
                                   * step(0.0, secondSdf);
                float overlapZone  = smoothstep(-0.005, 0.005, secondSdf);

                // ── Radial direction from owning bubble centre ─────────────
                float2 toOwner   = uv - ownCentre;
                float  distOwner = length(toOwner);
                float2 normDir   = (distOwner > 0.0001) ? (toOwner / distOwner)
                                                         : float2(1.0, 0.0);

                // ── Film mask ──────────────────────────────────────────────
                float filmMask = smoothstep(_FilmWidth, 0.0, bestSdf);

                // ── Iridescent film colour ─────────────────────────────────
                float  rimAngle  = atan2(normDir.y, normDir.x);
                float  hue       = frac(rimAngle / UNITY_TWO_PI + t * _IridescenceSpeed);
                fixed3 rainbow   = HsvToRgb(hue, _IridescenceSat, 1.0);
                fixed3 filmColor = lerp(IN.color.rgb, rainbow, _IridescenceAmt);

                filmColor = lerp(filmColor, fixed3(1.0, 1.0, 1.0), junctionLine * 0.80);
                filmColor = lerp(filmColor, fixed3(0.9, 0.95, 1.0), overlapZone  * 0.15);

                fixed3 interiorColor = IN.color.rgb * 0.55 + fixed3(0.3, 0.4, 0.5) * 0.25;

                // ── Specular highlight ─────────────────────────────────────
                // Computed in unrotated (uvRaw) space so the highlight direction
                // stays fixed relative to the light source as the cluster spins.
                // Inverse-rotate ownCentre (transpose of rotation matrix) to bring
                // it back into unrotated space before adding the fixed offset.
                float2 ownCentreUnrot = float2( ownCentre.x * cosR + ownCentre.y * sinR,
                                               -ownCentre.x * sinR + ownCentre.y * cosR);
                float2 specPos  = ownCentreUnrot + SceneLightDirection() * _SpecDistance;
                float  specDist = length(uvRaw - specPos);
                float  specMask = pow(saturate(1.0 - specDist / max(_SpecSize, 0.001)),
                                      _SpecSharpness);

                // ── Compose main layer ─────────────────────────────────────
                fixed3 col = lerp(interiorColor, filmColor, filmMask);

                // Specular sits inside the bubble where filmMask = 0, so it
                // must NOT be gated by filmMask.  We also boost alpha at the
                // specular position so it punches through the transparent interior.
                col    = lerp(col, _SpecColor.rgb, specMask * _SpecColor.a);

                float alpha = lerp(_InteriorAlpha, _FilmAlpha, filmMask);
                alpha += junctionLine * _JunctionLineAlpha  * (1.0 - filmMask);
                alpha += overlapZone  * _JunctionOverlapAlpha;
                alpha  = max(alpha, specMask * _SpecColor.a);
                alpha  = saturate(alpha) * IN.color.a;

                // ── Composite shadow behind main layer ─────────────────────
                #ifdef _SHADOW_ON
                fixed  combinedA   = alpha + shadowAlpha * (1.0 - alpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (col * alpha + _ShadowColor.rgb * shadowAlpha * (1.0 - alpha)) / combinedA
                    : col;
                return fixed4(combinedRGB, combinedA);
                #else
                return fixed4(col, alpha);
                #endif
            }
            ENDCG
        }
    }
}
