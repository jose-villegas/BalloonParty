Shader "BalloonParty/Grid/BushBakeLeaf"
{
    // Offline single-leaf baker — renders one Gielis leaf into a
    // RenderTexture for atlas packing. NOT used at runtime.
    //
    // Renders a single leaf at the origin with full shading:
    //   • Gielis superformula shape
    //   • SSS edge glow
    //   • Dome shading + specular highlight
    //   • Midrib + lateral veins
    //   • Hue jitter + edge browning
    //
    // No slot positions needed — the leaf is centred at (0,0) facing up.
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
        _CreaseWidth        ("Crease Width",         Range(0.01, 0.12))  = 0.07
        _CreaseDarken       ("Crease Darken",        Range(0.3, 1.0))    = 0.50

        [Header(Dome Shading)]
        _HighlightColor     ("Highlight Color",      Color)              = (0.55, 0.80, 0.35, 0.45)
        _HighlightSize      ("Highlight Size",       Range(0.1, 0.7))    = 0.30
        _HighlightOffset    ("Highlight Offset",     Range(-0.5, 0.5))   = 0.15
        _EdgeShade          ("Edge Shade",           Range(0.5, 1.0))    = 0.68

        [Header(Leaf Vein)]
        _VeinWidth          ("Vein Width",           Range(0.01, 0.15))  = 0.06
        _VeinDarken         ("Vein Darken",          Range(0.5, 1.0))    = 0.72
        _LateralVeinCount   ("Lateral Vein Count",   Range(3, 12))       = 6
        _LateralVeinAngle   ("Lateral Vein Angle",   Range(0.3, 3.0))    = 1.2

        [Header(Subsurface Scattering)]
        _SSSAbsorption      ("SSS Absorption",       Range(0.5, 10))     = 3.0
        _SSSStrength        ("SSS Strength",         Range(0, 1))        = 0.25
        _SSSColor           ("SSS Color",            Color)              = (0.6, 0.8, 0.2, 1.0)
        _LightDir           ("Light Direction",      Vector)             = (0.3, 0.7, 0, 0)

        [Header(Colour Variation)]
        _HueShift           ("Hue Shift (radians)",  Range(-0.2, 0.2))   = 0.0
        _BrowningColor      ("Browning Color",       Color)              = (0.40, 0.28, 0.12, 1.0)
        _EdgeBrowningWidth  ("Edge Browning Width",  Range(0.01, 0.5))   = 0.15

        [Header(Vein Texture)]
        _VeinTex            ("Vein Texture (opt)",   2D)                 = "white" {}
        _VeinTexStrength    ("Vein Tex Strength",    Range(0, 1))        = 0.0
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
            float  _CreaseWidth;
            float  _CreaseDarken;

            fixed4 _HighlightColor;
            float  _HighlightSize;
            float  _HighlightOffset;
            float  _EdgeShade;

            float  _LateralVeinCount;
            float  _LateralVeinAngle;
            float  _VeinWidth;
            float  _VeinDarken;

            float  _SSSAbsorption;
            float  _SSSStrength;
            fixed4 _SSSColor;
            float4 _LightDir;

            float  _HueShift;
            fixed4 _BrowningColor;
            float  _EdgeBrowningWidth;

            sampler2D _VeinTex;
            float  _VeinTexStrength;

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

                // ── Dome shading ──
                float edgeT = length(wp - center) / max(radius, 0.001);
                float radial = smoothstep(1.0, 0.3, edgeT);
                color *= lerp(_EdgeShade, 1.0, radial);

                // ── Leaf-local frame ──
                float2 tang = float2(-leafDir.y, leafDir.x);
                float halfLen = radius * GielisRadius(0.0, _GielisM, _GielisN1, _GielisN2, _GielisN3);
                float axial = dot(wp, leafDir);
                float perp  = dot(wp, tang);
                float stemAxisT = saturate((axial + halfLen) / max(2.0 * halfLen, 0.001));

                // ── SSS ──
                float thickness = saturate(-d / (radius * 0.5));
                float transmittance = exp(-thickness * _SSSAbsorption);
                float backLight = max(0, dot(-leafDir, _LightDir.xy));
                color = lerp(color, _SSSColor.rgb,
                    transmittance * _SSSStrength * (0.3 + backLight * 0.7));

                // ── Highlight ──
                float hlDist = length(float2(
                    (axial - _HighlightOffset * halfLen) / max(halfLen, 0.001),
                    perp / max(radius, 0.001)));
                float hlT = smoothstep(_HighlightSize, 0.0, hlDist);
                color = lerp(color, _HighlightColor.rgb, hlT * _HighlightColor.a);

                // ── Midrib ──
                float perpNorm = perp / max(radius, 0.001);
                float vDist = abs(perpNorm);
                float taperW = _VeinWidth * lerp(1.5, 0.3, stemAxisT);
                float vLine = 1.0 - smoothstep(taperW * 0.4, taperW, vDist);
                float vMask = smoothstep(0.02, 0.15, stemAxisT)
                            * smoothstep(0.98, 0.7, stemAxisT);
                color *= lerp(1.0, _VeinDarken, vLine * vMask);

                // ── Lateral veins — curved, bounded by leaf shape ──
                // Find the leaf's actual width at this axial position by
                // evaluating the Gielis boundary at the angle to the edge.
                float edgeAngle = atan2(1.0, 0.0); // +90° (leaf tip)
                float leafHalfWidth = radius * GielisRadius(
                    1.5708, _GielisM, _GielisN1, _GielisN2, _GielisN3);

                // Normalise perpendicular distance relative to actual leaf width
                float perpRelative = abs(perp) / max(leafHalfWidth * lerp(0.3, 1.0, stemAxisT), 0.001);

                // Curved vein model: each lateral follows a quadratic from midrib
                // curving toward the leaf tip as it moves outward.
                float veinParam = stemAxisT + perpRelative * perpRelative * 0.3;
                float veinField = veinParam * _LateralVeinCount;
                float veinFrac = frac(veinField);
                float veinD = min(veinFrac, 1.0 - veinFrac);

                // Vein width tapers from midrib to edge
                float lateralW = _VeinWidth * lerp(0.8, 0.15, perpRelative);
                float latLine = 1.0 - smoothstep(lateralW * 0.3, lateralW, veinD);

                // Only visible between midrib and leaf edge, fading at boundaries
                float latMask = smoothstep(0.05, 0.15, stemAxisT)
                    * smoothstep(0.98, 0.75, stemAxisT)
                    * smoothstep(0.03, 0.15, perpRelative)
                    * smoothstep(1.0, 0.7, perpRelative);
                color *= lerp(1.0, _VeinDarken, latLine * latMask);

                // ── Baked vein texture overlay ──
                if (_VeinTexStrength > 0.001)
                {
                    // Map world position to [0,1] UV matching the rasteriser's
                    // coordinate system: x ∈ [-radius, radius] → U, y ∈ [-radius, radius] → V
                    float2 veinUV = wp / (radius * 1.2) * 0.5 + 0.5;
                    fixed4 veinSample = tex2D(_VeinTex, veinUV);
                    color *= lerp(1.0, _VeinDarken, _VeinTexStrength * veinSample.a);
                }

                // ── Hue shift ──
                color = HueRotate(color, _HueShift);

                // ── Edge browning ──
                float edgeBrown = smoothstep(0.0, _EdgeBrowningWidth, -d / max(radius, 0.001));
                color = lerp(_BrowningColor.rgb, color, edgeBrown);

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

