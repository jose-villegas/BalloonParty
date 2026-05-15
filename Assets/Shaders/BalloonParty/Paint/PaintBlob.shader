Shader "BalloonParty/Paint/PaintBlob"
{
    Properties
    {
        _Color              ("Paint Color",          Color)              = (1, 0.2, 0.2, 1)

        [Header(Blob Shape)]
        _BlobRadius         ("Base Radius",          Range(0.10, 0.50))  = 0.40
        _EdgeSoftness       ("Edge Softness",        Range(0.001, 0.05)) = 0.012

        [Header(Wobble)]
        _TimeOffset         ("Time Offset",          Float)              = 0.0
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

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow", Float) = 0
        _ShadowColor        ("Shadow Color",   Color)              = (0.15, 0.15, 0.15, 0.6)
        _ShadowOffsetX      ("Shadow Offset X", Range(-0.20, 0.20)) = 0.02
        _ShadowOffsetY      ("Shadow Offset Y", Range(-0.20, 0.20)) = -0.03
        _ShadowSoftness     ("Shadow Softness", Range(0.001, 0.08)) = 0.02
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
            float  _TimeOffset;

            float  _RimWidth;
            float  _RimDarkness;
            fixed4 _SpecularColor;
            float  _SpecularSize;
            float  _SpecularSharpness;
            float  _SpecularOffsetX;
            float  _SpecularOffsetY;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowOffsetX;
            float  _ShadowOffsetY;
            float  _ShadowSoftness;
            #endif

            // Computes the blob SDF boundary at a given UV offset from center.
            // Returns the alpha (0 = outside, 1 = inside) using the wobble + edge softness.
            float BlobAlpha(float2 uv, float edgeSoftness)
            {
                float  r   = length(uv);
                float  t   = _Time.y + _TimeOffset;
                float2 dir = (r > 0.0001) ? (uv / r) : float2(1.0, 0.0);

                float2 z1 = float2(1.0, 0.0);
                int    n1 = (int)round(_WobbleFrequency);
                for (int i = 0; i < n1; i++)
                    z1 = float2(z1.x*dir.x - z1.y*dir.y, z1.x*dir.y + z1.y*dir.x);
                float w1 = z1.y * cos(_WobbleSpeed * t) + z1.x * sin(_WobbleSpeed * t);

                float2 z2 = float2(1.0, 0.0);
                int    n2 = (int)round(_WobbleFrequency2);
                for (int j = 0; j < n2; j++)
                    z2 = float2(z2.x*dir.x - z2.y*dir.y, z2.x*dir.y + z2.y*dir.x);
                float w2 = z2.y * cos(_WobbleSpeed2 * t) - z2.x * sin(_WobbleSpeed2 * t);

                float wobble   = _WobbleAmplitude * w1 + _WobbleAmplitude2 * w2;
                float boundary = _BlobRadius + wobble;
                float sdf      = boundary - r;
                return smoothstep(0.0, edgeSoftness, sdf);
            }

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
                float  t   = _Time.y + _TimeOffset;

                // ── Main blob alpha ──
                float alpha = BlobAlpha(uv, _EdgeSoftness);

                // ── Shadow (composited behind the blob) ──
                #ifdef _SHADOW_ON
                float2 shadowUV    = uv - float2(_ShadowOffsetX, _ShadowOffsetY);
                float  shadowAlpha = BlobAlpha(shadowUV, _ShadowSoftness) * _ShadowColor.a;
                #endif

                // Early discard — nothing to draw if both blob and shadow are invisible
                #ifdef _SHADOW_ON
                if (alpha < 0.001 && shadowAlpha < 0.001) discard;
                #else
                if (alpha < 0.001) discard;
                #endif

                // ── Blob surface shading ──
                float2 dir = (r > 0.0001) ? (uv / r) : float2(1.0, 0.0);

                float2 z1 = float2(1.0, 0.0);
                int    n1 = (int)round(_WobbleFrequency);
                for (int i = 0; i < n1; i++)
                    z1 = float2(z1.x*dir.x - z1.y*dir.y, z1.x*dir.y + z1.y*dir.x);
                float w1 = z1.y * cos(_WobbleSpeed  * t) + z1.x * sin(_WobbleSpeed  * t);

                float2 z2 = float2(1.0, 0.0);
                int    n2 = (int)round(_WobbleFrequency2);
                for (int j = 0; j < n2; j++)
                    z2 = float2(z2.x*dir.x - z2.y*dir.y, z2.x*dir.y + z2.y*dir.x);
                float w2 = z2.y * cos(_WobbleSpeed2 * t) - z2.x * sin(_WobbleSpeed2 * t);

                float wobble   = _WobbleAmplitude * w1 + _WobbleAmplitude2 * w2;
                float boundary = _BlobRadius + wobble;

                // Radial gradient for rim darkening
                float innerT  = saturate(r / max(boundary, 0.0001));
                float rimMask = pow(innerT, 1.0 / max(_RimWidth, 0.001));
                fixed3 col    = IN.color.rgb * (1.0 - rimMask * _RimDarkness);

                // Specular highlight
                float2 specCenter = float2(_SpecularOffsetX, _SpecularOffsetY);
                float  specDist   = length(uv - specCenter);
                float  specMask   = pow(saturate(1.0 - specDist / max(_SpecularSize, 0.001)),
                                        _SpecularSharpness);
                col = lerp(col, _SpecularColor.rgb, specMask * _SpecularColor.a);

                // ── Composite: shadow under blob (Porter-Duff "over") ──
                #ifdef _SHADOW_ON
                fixed3 shadowRGB = _ShadowColor.rgb;
                fixed  combinedA = alpha + shadowAlpha * (1.0 - alpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (col * alpha + shadowRGB * shadowAlpha * (1.0 - alpha)) / combinedA
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

