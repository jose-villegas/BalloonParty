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
