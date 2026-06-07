Shader "BalloonParty/Grid/BushBakeLeaf"
{
    // Offline single-leaf baker — renders one Gielis leaf into a
    // RenderTexture for atlas packing. NOT used at runtime.
    Properties
    {
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shape)]
        _LeafRadius         ("Leaf Radius",          Float)              = 0.40
        _AAWidth            ("AA Edge Width",        Range(0.001, 0.03)) = 0.008

        [Header(Gielis Superformula)]
        _GielisM            ("Lobe Count (m)",       Range(0, 6))        = 2.0
        _GielisN1           ("Curvature (n1)",       Range(0.1, 4))      = 1.0
        _GielisN2           ("Lateral Curve (n2)",   Range(0.1, 4))      = 1.5
        _GielisN3           ("Lateral Curve (n3)",   Range(0.1, 4))      = 1.5

        [Header(Surface)]
        _BaseColor          ("Base Color",           Color)              = (0.25, 0.55, 0.15, 1.0)
        _EdgeShade          ("Edge Shade",           Range(0.5, 1.0))    = 0.68

        [Header(Colour Variation)]
        _HueShift           ("Hue Shift (radians)",  Range(-0.2, 0.2))   = 0.0

        [Header(Midrib)]
        _MidribEnabled      ("Midrib Enabled",       Float)              = 1.0
        _MidribWidth        ("Midrib Width",         Range(0.001, 0.2))  = 0.04
        _MidribGradient     ("Midrib Gradient",      2D)                 = "white" {}

        [Header(Lateral Veins)]
        _LateralCount       ("Lateral Pair Count",   Float)              = 4
        _LateralAngleMin    ("Lateral Angle Min (rad)", Float)             = 0.698
        _LateralAngleMax    ("Lateral Angle Max (rad)", Float)             = 1.047
        _LateralWidth       ("Lateral Width",        Float)              = 0.024
        _LateralStart       ("Lateral Start",        Float)              = -0.6
        _LateralLengthMin   ("Lateral Length Min",   Float)              = 0.4
        _LateralLengthMax   ("Lateral Length Max",   Float)              = 0.8
        _LateralSubCount    ("Sub-vein Count",       Float)              = 2
        _LateralSubChance   ("Sub-vein Chance",      Float)              = 0.7
        _LateralSubLengthMin("Sub-vein Length Min",  Float)              = 0.15
        _LateralSubLengthMax("Sub-vein Length Max",  Float)              = 0.4
        _LateralCurvature   ("Lateral Curvature",    Range(0.0, 1.0))    = 0.3
        _SubVeinCurvature   ("Sub-vein Curvature",   Range(0.0, 1.0))    = 0.2
        _VeinSeed           ("Vein Seed",            Float)              = 0

        [Header(Reticulate)]
        _ReticulateEnabled  ("Reticulate Enabled",   Float)              = 1.0
        _ReticulateDensity  ("Reticulate Density",   Range(5.0, 60.0))   = 25.0
        _ReticulateWidth    ("Reticulate Width",     Range(0.01, 0.5))   = 0.15
        _ReticulateOpacity  ("Reticulate Opacity",   Range(0.0, 1.0))    = 0.12
        _ReticulateAngle    ("Reticulate Angle (rad)", Float)            = 0.7
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"
            #include "Assets/Shaders/BalloonParty/Grid/Editor/GielisSDF.cginc"

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

            fixed4 _RendererColor;

            float  _LeafRadius;
            float  _AAWidth;

            float  _GielisM;
            float  _GielisN1;
            float  _GielisN2;
            float  _GielisN3;

            fixed4 _BaseColor;
            float  _EdgeShade;

            float  _HueShift;

            float  _MidribEnabled;
            float  _MidribWidth;
            sampler2D _MidribGradient;

            float  _LateralCount;
            float  _LateralAngleMin;
            float  _LateralAngleMax;
            float  _LateralWidth;
            float  _LateralStart;
            float  _LateralLengthMin;
            float  _LateralLengthMax;
            float  _LateralSubCount;
            float  _LateralSubChance;
            float  _LateralSubLengthMin;
            float  _LateralSubLengthMax;
            float  _LateralCurvature;
            float  _SubVeinCurvature;
            float  _VeinSeed;

            float  _ReticulateEnabled;
            float  _ReticulateDensity;
            float  _ReticulateWidth;
            float  _ReticulateOpacity;
            float  _ReticulateAngle;

            // Deterministic hash for vein randomisation, seeded per variant
            float VeinHash(int a, int b, int c)
            {
                return frac(sin(float(a * 127 + b * 311 + c * 593) + _VeinSeed) * 43758.5453);
            }

            // Renders a single vein ray and returns the blended colour.
            // Also accumulates veinPresence for reticulate suppression.
            fixed3 ApplyVein(fixed3 col, float2 leafLocal, float2 origin,
                             float2 dir, float2 perp, float veinWidth,
                             float fadeLen, sampler2D gradTex, float fadeInAmount,
                             float radius, float gM, float gN1, float gN2, float gN3,
                             float sdfDist, float curvature,
                             inout float veinPresence)
            {
                float2 toFrag = leafLocal - origin;
                float along = dot(toFrag, dir);
                if (along > 0)
                {
                    float progress = along / max(fadeLen, 0.001);

                    // Fade in at origin (lerp between instant and smooth based on fadeInAmount)
                    float fadeIn  = lerp(1.0, smoothstep(0.0, 0.15, progress), fadeInAmount);
                    float fadeOut = 1.0 - smoothstep(0.8, 1.0, progress);

                    // Fade out near leaf edge (sdfDist is negative inside; closer to 0 = near edge)
                    float edgeFade = smoothstep(0.0, -0.04, sdfDist);

                    float fade = fadeIn * fadeOut * edgeFade;

                    // Taper width: narrow at tip, optionally at origin
                    float originTaper = lerp(1.0, fadeIn, fadeInAmount);
                    float taper = originTaper * lerp(1.0, 0.4, progress);
                    float taperW = veinWidth * taper;

                    // Shape-adaptive curvature: sample the Gielis boundary at
                    // the vein's straight-line position and offset toward it.
                    float2 straightPos = origin + dir * along;
                    float theta = atan2(straightPos.x, straightPos.y);
                    float boundary = radius * GielisRadius(theta, gM, gN1, gN2, gN3);
                    float2 boundaryPt = float2(sin(theta), cos(theta)) * boundary;
                    float2 toBoundary = boundaryPt - straightPos;
                    float curveOffset = dot(toBoundary, perp) * curvature * progress;

                    float perpDist = dot(toFrag, perp) - curveOffset;
                    float latT = saturate(perpDist / max(taperW, 0.0001) * 0.5 + 0.5);
                    fixed4 g = tex2D(gradTex, float2(latT, 0.5));
                    float contribution = g.a * fade;
                    col = lerp(col, g.rgb, contribution);

                    // Accumulate vein presence with a soft halo around the vein
                    float haloWidth = veinWidth * 3.0;
                    float proxDist = abs(perpDist) / max(haloWidth, 0.0001);
                    float proximity = (1.0 - saturate(proxDist)) * fade;
                    veinPresence = max(veinPresence, proximity);
                }
                return col;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;

                // Leaf centred at origin, pointing up (+Y)
                float2 center = float2(0, 0);
                float2 leafDir = float2(0, 1);
                float radius = _LeafRadius;

                float d = GielisSDF(wp, center, radius, leafDir,
                                    _GielisM, _GielisN1, _GielisN2, _GielisN3);

                if (d > _AAWidth) discard;

                fixed3 color = _BaseColor.rgb;

                // ── Dome shading — simple radial darkening at edges ──
                float edgeT = length(wp - center) / max(radius, 0.001);
                float radial = smoothstep(1.0, 0.3, edgeT);
                color *= lerp(_EdgeShade, 1.0, radial);

                // ── Vein presence tracker for reticulate suppression ──
                float veinPresence = 0;

                // ── Midrib system — adapts to lobe count ──
                // m<2.5: single vertical midrib (pinnate); m≥2.5: one midrib per lobe (palmate)
                if (_MidribEnabled > 0.5)
                {
                    float2 leafLocal = wp - center;
                    int numMidribs = _GielisM < 2.5 ? 1 : (int)floor(_GielisM);

                    [loop] for (int mr = 0; mr < 6; mr++)
                    {
                        if (mr >= numMidribs) break;

                        // Midrib direction: for single midrib, point up; for multi, radiate equally
                        float midribAngle = numMidribs == 1 ? 0.0 : (float(mr) * 6.28318 / float(numMidribs));
                        float2 md = float2(sin(midribAngle), cos(midribAngle));
                        float2 mt = float2(-md.y, md.x); // perpendicular to midrib

                        float v = dot(leafLocal, mt);

                        // For multi-midrib, only render where fragment is on this lobe's side
                        float alongMidrib = dot(leafLocal, md);
                        if (numMidribs > 1 && alongMidrib < -_MidribWidth) continue;

                        // Signed distance mapped to [0,1] across the full vein width
                        float veinT = saturate(v / max(_MidribWidth, 0.0001) * 0.5 + 0.5);

                        fixed4 grad = tex2D(_MidribGradient, float2(veinT, 0.5));
                        color = lerp(color, grad.rgb, grad.a);

                        // Midrib presence: soft halo at 2x width
                        float midribProx = 1.0 - saturate(abs(v) / max(_MidribWidth * 2.0, 0.0001));
                        if (numMidribs > 1) midribProx *= saturate(alongMidrib / (_MidribWidth * 2.0));
                        veinPresence = max(veinPresence, midribProx);

                        // ── Lateral veins — mirrored pairs branching from this midrib ──
                        int count = (int)_LateralCount;
                        int subCount = (int)_LateralSubCount;
                        if (count > 0)
                        {
                            float subWidth = _LateralWidth * 0.5;

                            [loop] for (int i = 0; i < 8; i++)
                            {
                                if (i >= count) break;

                                float t = (float(i) + 1.0) / (float(count) + 1.0);
                                float originU = lerp(radius * _LateralStart, radius * 0.8, t);
                                float2 veinOrigin = md * originU;

                                // Per-lateral randomised angle
                                float angle = lerp(_LateralAngleMin, _LateralAngleMax, VeinHash(i + mr * 8, 0, 23));
                                float sa, ca;
                                sincos(angle, sa, ca);
                                // Rotate md by ±angle for left/right laterals
                                float2 leftDir  = float2(ca * md.x - sa * md.y, sa * md.x + ca * md.y);
                                float2 rightDir = float2(ca * md.x + sa * md.y, -sa * md.x + ca * md.y);
                                float2 leftPerp  = float2(-leftDir.y, leftDir.x);
                                float2 rightPerp = float2(-rightDir.y, rightDir.x);

                                // Per-lateral randomised length, biased by position
                                float baseBias = 1.0 - t;
                                float fadeLenL = radius * lerp(_LateralLengthMin, _LateralLengthMax, VeinHash(i + mr * 8, 0, 7) * baseBias);
                                float fadeLenR = radius * lerp(_LateralLengthMin, _LateralLengthMax, VeinHash(i + mr * 8, 0, 11) * baseBias);

                                // Primary left lateral
                                color = ApplyVein(color, leafLocal, veinOrigin,
                                                  leftDir, leftPerp, _LateralWidth,
                                                  fadeLenL, _MidribGradient, 0,
                                                  radius, _GielisM, _GielisN1, _GielisN2, _GielisN3, d, _LateralCurvature, veinPresence);

                                // Primary right lateral
                                color = ApplyVein(color, leafLocal, veinOrigin,
                                                  rightDir, rightPerp, _LateralWidth,
                                                  fadeLenR, _MidribGradient, 0,
                                                  radius, _GielisM, _GielisN1, _GielisN2, _GielisN3, d, _LateralCurvature, veinPresence);

                                // ── Venules branching from this lateral pair ──
                                float subSa, subCa;
                                sincos(angle, subSa, subCa);

                                float2 subLL_dir  = float2(subCa * leftDir.x - subSa * leftDir.y,
                                                           subSa * leftDir.x + subCa * leftDir.y);
                                float2 subLR_dir  = float2(subCa * leftDir.x + subSa * leftDir.y,
                                                          -subSa * leftDir.x + subCa * leftDir.y);
                                float2 subLL_perp = float2(-subLL_dir.y, subLL_dir.x);
                                float2 subLR_perp = float2(-subLR_dir.y, subLR_dir.x);

                                float2 subRL_dir  = float2(subCa * rightDir.x - subSa * rightDir.y,
                                                           subSa * rightDir.x + subCa * rightDir.y);
                                float2 subRR_dir  = float2(subCa * rightDir.x + subSa * rightDir.y,
                                                          -subSa * rightDir.x + subCa * rightDir.y);
                                float2 subRL_perp = float2(-subRL_dir.y, subRL_dir.x);
                                float2 subRR_perp = float2(-subRR_dir.y, subRR_dir.x);

                                [loop] for (int j = 0; j < 4; j++)
                                {
                                    if (j >= subCount) break;

                                    float stL = (float(j) + 1.0) / (float(subCount) + 1.0);
                                    float subAlongDistL = fadeLenL * stL * 0.8;
                                    float subAlongDistR = fadeLenR * stL * 0.8;

                                    // Offset sub-vein origins along the curved parent lateral
                                    float2 straightL = veinOrigin + leftDir * subAlongDistL;
                                    float thetaL = atan2(straightL.x, straightL.y);
                                    float bndL = radius * GielisRadius(thetaL, _GielisM, _GielisN1, _GielisN2, _GielisN3);
                                    float2 toBndL = float2(sin(thetaL), cos(thetaL)) * bndL - straightL;
                                    float curveOffL = dot(toBndL, leftPerp) * _LateralCurvature * stL * 0.8;
                                    float2 subOriginL = straightL + leftPerp * curveOffL;

                                    float2 straightR = veinOrigin + rightDir * subAlongDistR;
                                    float thetaR = atan2(straightR.x, straightR.y);
                                    float bndR = radius * GielisRadius(thetaR, _GielisM, _GielisN1, _GielisN2, _GielisN3);
                                    float2 toBndR = float2(sin(thetaR), cos(thetaR)) * bndR - straightR;
                                    float curveOffR = dot(toBndR, rightPerp) * _LateralCurvature * stL * 0.8;
                                    float2 subOriginR = straightR + rightPerp * curveOffR;

                                    // Per-sub-vein randomised length, biased by position along parent
                                    float subBias = 1.0 - stL;
                                    float subFadeLL = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i + mr * 8, j, 9) * subBias);
                                    float subFadeLR = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i + mr * 8, j, 13) * subBias);
                                    float subFadeRL = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i + mr * 8, j, 17) * subBias);
                                    float subFadeRR = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i + mr * 8, j, 21) * subBias);

                                    // Left lateral → two sub-veins (each side of parent)
                                    if (VeinHash(i + mr * 8, j, 0) < _LateralSubChance)
                                    {
                                        color = ApplyVein(color, leafLocal, subOriginL,
                                                          subLL_dir, subLL_perp, subWidth,
                                                          subFadeLL, _MidribGradient, 1,
                                                          radius, _GielisM, _GielisN1, _GielisN2, _GielisN3, d, _SubVeinCurvature, veinPresence);
                                    }
                                    if (VeinHash(i + mr * 8, j, 2) < _LateralSubChance)
                                    {
                                        color = ApplyVein(color, leafLocal, subOriginL,
                                                          subLR_dir, subLR_perp, subWidth,
                                                          subFadeLR, _MidribGradient, 1,
                                                          radius, _GielisM, _GielisN1, _GielisN2, _GielisN3, d, _SubVeinCurvature, veinPresence);
                                    }

                                    // Right lateral → two sub-veins (each side of parent)
                                    if (VeinHash(i + mr * 8, j, 1) < _LateralSubChance)
                                    {
                                        color = ApplyVein(color, leafLocal, subOriginR,
                                                          subRL_dir, subRL_perp, subWidth,
                                                          subFadeRL, _MidribGradient, 1,
                                                          radius, _GielisM, _GielisN1, _GielisN2, _GielisN3, d, _SubVeinCurvature, veinPresence);
                                    }
                                    if (VeinHash(i + mr * 8, j, 3) < _LateralSubChance)
                                    {
                                        color = ApplyVein(color, leafLocal, subOriginR,
                                                          subRR_dir, subRR_perp, subWidth,
                                                          subFadeRR, _MidribGradient, 1,
                                                          radius, _GielisM, _GielisN1, _GielisN2, _GielisN3, d, _SubVeinCurvature, veinPresence);
                                    }
                                }
                            }
                        }
                    }
                }

                // ── Reticulate venation — fine net pattern between veins ──
                if (_ReticulateEnabled > 0.5)
                {
                    float2 leafLocal2 = wp - center;

                    // Organic distortion: offset coordinates with cheap noise
                    float nx = sin(leafLocal2.x * 31.7 + leafLocal2.y * 17.3) * 0.3
                             + sin(leafLocal2.y * 23.1 - leafLocal2.x * 11.9) * 0.2;
                    float ny = sin(leafLocal2.y * 29.3 + leafLocal2.x * 13.7) * 0.3
                             + sin(leafLocal2.x * 19.7 - leafLocal2.y * 7.1) * 0.2;
                    float2 distorted = leafLocal2 + float2(nx, ny) / _ReticulateDensity;

                    // Two sets of parallel lines at ±angle create a mesh
                    float rSa, rCa;
                    sincos(_ReticulateAngle, rSa, rCa);
                    float2 retDir1 = float2(rCa, rSa);
                    float2 retDir2 = float2(rCa, -rSa);

                    float proj1 = dot(distorted, retDir1) * _ReticulateDensity;
                    float proj2 = dot(distorted, retDir2) * _ReticulateDensity;

                    // Distance to nearest line in each set (0 = on line, 0.5 = between)
                    float d1 = abs(frac(proj1) - 0.5) * 2.0;
                    float d2 = abs(frac(proj2) - 0.5) * 2.0;

                    // Thin lines with noise-varied width for irregularity
                    float widthVar = 1.0 + sin(proj1 * 3.7 + proj2 * 2.3) * 0.3;
                    float w = _ReticulateWidth * widthVar;
                    float line1 = 1.0 - smoothstep(0.0, w, d1);
                    float line2 = 1.0 - smoothstep(0.0, w, d2);

                    // Combine both sets: union of lines
                    float net = max(line1, line2);

                    // Fade near leaf edge and suppress near existing veins
                    float edgeFade = smoothstep(0.0, -0.06, d);
                    float veinSuppress = 1.0 - veinPresence;

                    // Apply as darkening, respecting vein zones
                    float darken = 1.0 - net * _ReticulateOpacity * edgeFade * veinSuppress;
                    color *= darken;
                }

                // ── Hue shift ──
                color = HueRotate(color, _HueShift);

                // ── Alpha + premultiplied output ──
                float alpha = 1.0 - smoothstep(-_AAWidth, 0.0, d);
                float finalAlpha = alpha * IN.color.a;
                fixed3 finalRgb = color * IN.color.rgb;

                return fixed4(finalRgb * finalAlpha, finalAlpha);
            }
            ENDCG
        }
    }
}
