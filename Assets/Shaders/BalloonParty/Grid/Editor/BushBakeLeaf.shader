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
        _LateralAngle       ("Lateral Angle (rad)",  Float)              = 0.785
        _LateralWidth       ("Lateral Width",        Float)              = 0.024
        _LateralStart       ("Lateral Start",        Float)              = -0.6
        _LateralLength      ("Lateral Length",       Float)              = 0.7
        _LateralSubCount    ("Sub-vein Count",       Float)              = 2
        _LateralSubChance   ("Sub-vein Chance",      Float)              = 0.7
        _LateralSubLength   ("Sub-vein Length",      Float)              = 0.35
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
            float  _LateralAngle;
            float  _LateralWidth;
            float  _LateralStart;
            float  _LateralLength;
            float  _LateralSubCount;
            float  _LateralSubChance;
            float  _LateralSubLength;

            // Deterministic hash for sub-vein survival
            float VeinHash(int a, int b, int c)
            {
                return frac(sin(float(a * 127 + b * 311 + c * 593)) * 43758.5453);
            }

            // Renders a single vein ray and returns the blended colour.
            // dir = vein direction, perp = perpendicular across width,
            // origin = vein origin in leaf-local space.
            fixed3 ApplyVein(fixed3 col, float2 leafLocal, float2 origin,
                             float2 dir, float2 perp, float veinWidth,
                             float fadeLen, sampler2D gradTex)
            {
                float2 toFrag = leafLocal - origin;
                float along = dot(toFrag, dir);
                if (along > 0)
                {
                    float perpDist = dot(toFrag, perp);
                    float latT = saturate(perpDist / max(veinWidth, 0.0001) * 0.5 + 0.5);
                    fixed4 g = tex2D(gradTex, float2(latT, 0.5));
                    float fade = 1.0 - saturate(along / max(fadeLen, 0.001));
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
                        float sa, ca;
                        sincos(_LateralAngle, sa, ca);
                        float2 leftDir  = float2(-sa, ca);
                        float2 rightDir = float2( sa, ca);
                        float2 leftPerp  = float2(-ca, -sa);
                        float2 rightPerp = float2(-ca,  sa);

                        float fadeLen = radius * _LateralLength;
                        float subWidth = _LateralWidth * 0.5;
                        float subFadeLen = radius * _LateralSubLength;

                        // Sub-vein directions relative to each parent lateral
                        // Rotate parent dir by ±angle to get two sub-directions
                        float subSa, subCa;
                        sincos(_LateralAngle, subSa, subCa);

                        // For left parent lateral: rotate leftDir by ±angle
                        float2 subLL_dir  = float2(subCa * leftDir.x - subSa * leftDir.y,
                                                   subSa * leftDir.x + subCa * leftDir.y);
                        float2 subLR_dir  = float2(subCa * leftDir.x + subSa * leftDir.y,
                                                  -subSa * leftDir.x + subCa * leftDir.y);
                        float2 subLL_perp = float2(-subLL_dir.y, subLL_dir.x);
                        float2 subLR_perp = float2(-subLR_dir.y, subLR_dir.x);

                        // For right parent lateral: rotate rightDir by ±angle
                        float2 subRL_dir  = float2(subCa * rightDir.x - subSa * rightDir.y,
                                                   subSa * rightDir.x + subCa * rightDir.y);
                        float2 subRR_dir  = float2(subCa * rightDir.x + subSa * rightDir.y,
                                                  -subSa * rightDir.x + subCa * rightDir.y);
                        float2 subRL_perp = float2(-subRL_dir.y, subRL_dir.x);
                        float2 subRR_perp = float2(-subRR_dir.y, subRR_dir.x);

                        for (int i = 0; i < 8; i++)
                        {
                            if (i >= count) break;

                            float t = (float(i) + 1.0) / (float(count) + 1.0);
                            float originU = lerp(radius * _LateralStart, radius * 0.8, t);
                            float2 veinOrigin = leafDir * originU;

                            // Primary left lateral
                            color = ApplyVein(color, leafLocal, veinOrigin,
                                              leftDir, leftPerp, _LateralWidth,
                                              fadeLen, _MidribGradient);

                            // Primary right lateral
                            color = ApplyVein(color, leafLocal, veinOrigin,
                                              rightDir, rightPerp, _LateralWidth,
                                              fadeLen, _MidribGradient);

                            // ── Sub-veins branching from this lateral pair ──
                            for (int j = 0; j < 4; j++)
                            {
                                if (j >= subCount) break;

                                float st = (float(j) + 1.0) / (float(subCount) + 1.0);
                                float subAlongDist = fadeLen * st * 0.8;
                                float2 subOriginL = veinOrigin + leftDir * subAlongDist;
                                float2 subOriginR = veinOrigin + rightDir * subAlongDist;

                                // Left lateral → two sub-veins (each side of parent)
                                if (VeinHash(i, j, 0) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginL,
                                                      subLL_dir, subLL_perp, subWidth,
                                                      subFadeLen, _MidribGradient);
                                }
                                if (VeinHash(i, j, 2) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginL,
                                                      subLR_dir, subLR_perp, subWidth,
                                                      subFadeLen, _MidribGradient);
                                }

                                // Right lateral → two sub-veins (each side of parent)
                                if (VeinHash(i, j, 1) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginR,
                                                      subRL_dir, subRL_perp, subWidth,
                                                      subFadeLen, _MidribGradient);
                                }
                                if (VeinHash(i, j, 3) < _LateralSubChance)
                                {
                                    color = ApplyVein(color, leafLocal, subOriginR,
                                                      subRR_dir, subRR_perp, subWidth,
                                                      subFadeLen, _MidribGradient);
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
