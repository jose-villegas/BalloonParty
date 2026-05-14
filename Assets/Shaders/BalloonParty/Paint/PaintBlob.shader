Shader "BalloonParty/Paint/PaintBlob"
{
    Properties
    {
        _Color              ("Paint Color",          Color)              = (1, 0.2, 0.2, 1)

        [Header(Blob Shape)]
        _BlobRadius         ("Base Radius",          Range(0.10, 0.50))  = 0.40
        _EdgeSoftness       ("Edge Softness",        Range(0.001, 0.05)) = 0.012

        [Header(Wobble)]
        _WobbleSpeed        ("Speed",                Range(0, 6))        = 1.4
        _WobbleAmplitude    ("Amplitude",            Range(0, 0.08))     = 0.028
        _WobbleFrequency    ("Frequency  (lobes)",   Range(1, 8))        = 4.0
        _WobbleSpeed2       ("Speed 2",              Range(0, 6))        = 2.3
        _WobbleAmplitude2   ("Amplitude 2",          Range(0, 0.04))     = 0.012
        _WobbleFrequency2   ("Frequency 2  (lobes)", Range(1, 12))       = 7.0

        [Header(Surface)]
        _RimWidth           ("Rim Width",            Range(0.05, 2.0))   = 0.55
        _RimDarkness        ("Rim Darkness",         Range(0, 1))        = 0.45
        _SpecularColor      ("Specular Color",       Color)              = (1, 1, 1, 0.85)
        _SpecularSize       ("Specular Size",        Range(0.01, 0.40))  = 0.14
        _SpecularSharpness  ("Specular Sharpness",   Range(1, 20))       = 7.0
        _SpecularOffsetX    ("Specular Offset X",    Range(-0.40, 0.40)) = -0.14
        _SpecularOffsetY    ("Specular Offset Y",    Range(-0.40, 0.40)) =  0.18
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

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
            };

            fixed4 _Color;

            float  _BlobRadius;
            float  _EdgeSoftness;

            float  _WobbleSpeed;
            float  _WobbleAmplitude;
            float  _WobbleFrequency;
            float  _WobbleSpeed2;
            float  _WobbleAmplitude2;
            float  _WobbleFrequency2;

            float  _RimWidth;
            float  _RimDarkness;
            fixed4 _SpecularColor;
            float  _SpecularSize;
            float  _SpecularSharpness;
            float  _SpecularOffsetX;
            float  _SpecularOffsetY;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv  = IN.texcoord - 0.5;
                float  r   = length(uv);
                float  phi = atan2(uv.y, uv.x);
                float  t   = _Time.y;

                // Two sine-wave octaves counter-rotating so the blob never settles into
                // a repeating lobe pattern — gives a more organic, floating-paint feel.
                float wobble  = _WobbleAmplitude  * sin(_WobbleFrequency  * phi + _WobbleSpeed  * t)
                              + _WobbleAmplitude2 * sin(_WobbleFrequency2 * phi - _WobbleSpeed2 * t);
                float boundary = _BlobRadius + wobble;

                // Signed distance: positive inside the blob, negative outside.
                float sdf   = boundary - r;
                float alpha = smoothstep(0.0, _EdgeSoftness, sdf);
                if (alpha < 0.001) discard;

                // Radial gradient from center (0) to edge (1) — drives the rim darkening
                // to fake the curvature of a sphere without a 3-D normal.
                float innerT  = saturate(r / max(boundary, 0.0001));
                float rimMask = pow(innerT, 1.0 / max(_RimWidth, 0.001));
                fixed3 col    = IN.color.rgb * (1.0 - rimMask * _RimDarkness);

                // Single offset specular highlight to sell the wet, glossy paint surface.
                float2 specCenter = float2(_SpecularOffsetX, _SpecularOffsetY);
                float  specDist   = length(uv - specCenter);
                float  specMask   = pow(saturate(1.0 - specDist / max(_SpecularSize, 0.001)),
                                        _SpecularSharpness);
                col = lerp(col, _SpecularColor.rgb, specMask * _SpecularColor.a);

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
}

