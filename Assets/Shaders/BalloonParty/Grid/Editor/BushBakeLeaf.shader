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

            // Deterministic hash for sub-vein survival
            float VeinHash(int a, int b, int c)
            {
                return frac(sin(float(a * 127 + b * 311 + c * 593)) * 43758.5453);
            }

            // Renders a single vein ray and returns the blended colour.
            // fadeInAmount: 0 = no origin fade (emerges from parent), 1 = full fade-in
            fixed3 ApplyVein(fixed3 col, float2 leafLocal, float2 origin,
                             float2 dir, float2 perp, float veinWidth,
                             float fadeLen, sampler2D gradTex, float fadeInAmount)
            {
                float2 toFrag = leafLocal - origin;
                float along = dot(toFrag, dir);
                if (along > 0)
                {
                    float progress = along / max(fadeLen, 0.001);

                    // Fade in at origin (lerp between instant and smooth based on fadeInAmount)
                    float fadeIn  = lerp(1.0, smoothstep(0.0, 0.15, progress), fadeInAmount);
                    float fadeOut = 1.0 - smoothstep(0.8, 1.0, progress);
                    float fade = fadeIn * fadeOut;

                    // Taper width: narrow at tip, optionally at origin
                    float originTaper = lerp(1.0, fadeIn, fadeInAmount);
                    float taper = originTaper * lerp(1.0, 0.4, progress);
                    float taperW = veinWidth * taper;

                    float perpDist = dot(toFrag, perp);
                    float latT = saturate(perpDist / max(taperW, 0.0001) * 0.5 + 0.5);
                    fixed4 g = tex2D(gradTex, float2(latT, 0.5));
                    col = lerp(col, g.rgb, g.a * fade);
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

                // ── Midrib — gradient-driven vein across its width ──
                // Gradient maps left-to-right: 0% = left edge, 50% = centre, 100% = right edge
                if (_MidribEnabled > 0.5)
                {
                    float2 leafLocal = wp - center;
                    float2 tang = float2(-leafDir.y, leafDir.x);
                    float v = dot(leafLocal, tang);

                    // Signed distance mapped to [0,1] across the full vein width
                    float veinT = saturate(v / max(_MidribWidth, 0.0001) * 0.5 + 0.5);

                    fixed4 grad = tex2D(_MidribGradient, float2(veinT, 0.5));
                    color = lerp(color, grad.rgb, grad.a);

                    // ── Lateral veins — mirrored pairs branching from the midrib ──
                    int count = (int)_LateralCount;
                    int subCount = (int)_LateralSubCount;
                    if (count > 0)
                    {
                        float subWidth = _LateralWidth * 0.5;

                        for (int i = 0; i < 8; i++)
                        {
                            if (i >= count) break;

                            float t = (float(i) + 1.0) / (float(count) + 1.0);
                            float originU = lerp(radius * _LateralStart, radius * 0.8, t);
                            float2 veinOrigin = leafDir * originU;

                            // Per-lateral randomised angle
                            float angle = lerp(_LateralAngleMin, _LateralAngleMax, VeinHash(i, 0, 23));
                            float sa, ca;
                            sincos(angle, sa, ca);
                            float2 leftDir  = float2(-sa, ca);
                            float2 rightDir = float2( sa, ca);
                            float2 leftPerp  = float2(-ca, -sa);
                            float2 rightPerp = float2(-ca,  sa);

                            // Per-lateral randomised length, biased by position
                            float baseBias = 1.0 - t;
                            float fadeLenL = radius * lerp(_LateralLengthMin, _LateralLengthMax, VeinHash(i, 0, 7) * baseBias);
                            float fadeLenR = radius * lerp(_LateralLengthMin, _LateralLengthMax, VeinHash(i, 0, 11) * baseBias);

                            // Primary left lateral
                            color = ApplyVein(color, leafLocal, veinOrigin,
                                              leftDir, leftPerp, _LateralWidth,
                                              fadeLenL, _MidribGradient, 0);

                            // Primary right lateral
                            color = ApplyVein(color, leafLocal, veinOrigin,
                                              rightDir, rightPerp, _LateralWidth,
                                              fadeLenR, _MidribGradient, 0);

                            // ── Sub-veins branching from this lateral pair ──
                            // Compute sub-vein directions from this lateral's angle
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

                            for (int j = 0; j < 4; j++)
                            {
                                if (j >= subCount) break;

                                float stL = (float(j) + 1.0) / (float(subCount) + 1.0);
                                float subAlongDistL = fadeLenL * stL * 0.8;
                                float subAlongDistR = fadeLenR * stL * 0.8;
                                float2 subOriginL = veinOrigin + leftDir * subAlongDistL;
                                float2 subOriginR = veinOrigin + rightDir * subAlongDistR;

                                // Per-sub-vein randomised length, biased by position along parent
                                float subBias = 1.0 - stL;
                                float subFadeLL = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i, j, 9) * subBias);
                                float subFadeLR = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i, j, 13) * subBias);
                                float subFadeRL = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i, j, 17) * subBias);
                                float subFadeRR = radius * lerp(_LateralSubLengthMin, _LateralSubLengthMax, VeinHash(i, j, 21) * subBias);

                                // Left lateral → two sub-veins (each side of parent)
                                if (VeinHash(i, j, 0) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginL,
                                                      subLL_dir, subLL_perp, subWidth,
                                                      subFadeLL, _MidribGradient, 1);
                                }
                                if (VeinHash(i, j, 2) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginL,
                                                      subLR_dir, subLR_perp, subWidth,
                                                      subFadeLR, _MidribGradient, 1);
                                }

                                // Right lateral → two sub-veins (each side of parent)
                                if (VeinHash(i, j, 1) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginR,
                                                      subRL_dir, subRL_perp, subWidth,
                                                      subFadeRL, _MidribGradient, 1);
                                }
                                if (VeinHash(i, j, 3) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginR,
                                                      subRR_dir, subRR_perp, subWidth,
                                                      subFadeRR, _MidribGradient, 1);
                                }
                            }
                        }
                    }
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
