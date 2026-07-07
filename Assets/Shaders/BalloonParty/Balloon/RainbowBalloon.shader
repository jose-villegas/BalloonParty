Shader "BalloonParty/Balloon/RainbowBalloon"
{
    // A "rainbow star" balloon: the flat sprite tint is replaced by scrolling diagonal
    // colour bands cycling through up to four selectable colours. Colour count + values are
    // meant to be driven at runtime from the level's allowed colours (via MaterialPropertyBlock),
    // but default to the full palette so the material previews standalone. Keeps SpriteShineShadow's
    // diagonal shine sweep; no drop shadow. An optional UV-rect mask excludes a region (e.g. the
    // balloon's knot) from the band tint, so it reads as a stable part of the sprite instead of
    // being cut across by the scroll.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Bands)]
        _Color0 ("Colour 0", Color) = (0.467, 0.867, 0.467, 1)
        _Color1 ("Colour 1", Color) = (1.0, 0.412, 0.380, 1)
        _Color2 ("Colour 2", Color) = (0.012, 0.663, 0.957, 1)
        _Color3 ("Colour 3", Color) = (0.612, 0.153, 0.690, 1)
        _BandCount   ("Colour Count",  Range(1, 4))  = 4
        _StripeCount ("Stripe Density", Range(1, 12)) = 5
        _ScrollSpeed ("Scroll Speed",  Range(-5, 5)) = 1.0
        _BandBlend   ("Edge Softness", Range(0, 0.5)) = 0.08
        _BandAngle   ("Angle (turns)", Range(0, 1))  = 0.125
        [HideInInspector] _TimeOffset ("Time Offset", Float) = 0

        [Header(Mask)]
        // UV-space rectangle excluded from the band tint (e.g. the knot). Zero-size = no mask.
        _MaskMin ("Mask Min (UV)", Vector) = (0, 0, 0, 0)
        _MaskMax ("Mask Max (UV)", Vector) = (0, 0, 0, 0)
        _MaskSoftness ("Mask Edge Softness", Range(0, 0.2)) = 0.02

        [Header(Shine)]
        _ShineWidth    ("Width",    Range(0, 1))   = 0.1
        _ShineSpeed    ("Speed",    Range(0, 5))   = 1.0
        _ShineInterval ("Interval", Range(0, 10))  = 3.0
        _ShineAngle    ("Angle (turns)", Range(0, 1)) = 0.125

        [Header(Sprite)]
        _SpriteScale ("Scale", Range(0.1, 1.0)) = 1.0

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   2.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float     _SpriteScale;
            float     _ShineWidth;
            float     _ShineSpeed;
            float     _ShineInterval;
            float     _ShineAngle;

            fixed4 _Color0;
            fixed4 _Color1;
            fixed4 _Color2;
            fixed4 _Color3;
            float  _BandCount;
            float  _StripeCount;
            float  _ScrollSpeed;
            float  _BandBlend;
            float  _BandAngle;
            float  _TimeOffset;

            float2 _MaskMin;
            float2 _MaskMax;
            float  _MaskSoftness;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv     = IN.uv;
                // Carries the SpriteRenderer's per-frame alpha (spawn/despawn fades) but NOT a
                // palette tint — the bands provide the colour.
                OUT.color  = IN.color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            inline fixed3 ColorAt(int i)
            {
                if (i <= 0) { return _Color0.rgb; }
                if (i == 1) { return _Color1.rgb; }
                if (i == 2) { return _Color2.rgb; }
                return _Color3.rgb;
            }

            // 0..1 position along a diagonal at angleTurns (0..1 turns), centred on the UV square.
            inline float DiagonalProjection(float2 uv, float angleTurns)
            {
                float ang = angleTurns * 6.2831853;
                float2 dir = float2(cos(ang), sin(ang));
                return dot(uv - 0.5, dir) + 0.5;
            }

            // Diagonal scrolling colour bands cycling through the first _BandCount colours.
            inline fixed3 RainbowBand(float2 uv)
            {
                float projection = DiagonalProjection(uv, _BandAngle);

                float s = projection * _StripeCount + _Time.y * _ScrollSpeed + _TimeOffset;
                float cell = floor(s);
                float t = frac(s);

                float n = max(_BandCount, 1.0);
                float m = fmod(fmod(cell, n) + n, n); // always in [0, n), even for negative scroll
                int i0 = (int)m;
                int i1 = (int)fmod(m + 1.0, n);

                float edge = max(_BandBlend, 1e-4);
                float blend = smoothstep(1.0 - edge, 1.0, t);
                return lerp(ColorAt(i0), ColorAt(i1), blend);
            }

            // 1 inside [_MaskMin, _MaskMax] (band excluded there), 0 outside, soft edge in between.
            // Defaults to a zero-size rect, so the mask is off unless deliberately configured.
            inline float MaskAmount(float2 uv)
            {
                float2 lowerEdge = smoothstep(_MaskMin - _MaskSoftness, _MaskMin + _MaskSoftness, uv);
                float2 upperEdge = 1.0 - smoothstep(_MaskMax - _MaskSoftness, _MaskMax + _MaskSoftness, uv);
                float2 inside = lowerEdge * upperEdge;
                return inside.x * inside.y;
            }

            // Additive white shine sweep (0..1) — same diagonal band shape as SpriteShineShadow, angle tunable.
            inline fixed ShineAmount(float2 uv)
            {
                float sweepDuration = 1.0 / max(_ShineSpeed, 0.001);
                float cycleDuration = sweepDuration + _ShineInterval;
                float t = fmod(_Time.y, cycleDuration);
                float shineLocation = -_ShineWidth + (1.0 + 2.0 * _ShineWidth) * saturate(t / sweepDuration);

                float projection = DiagonalProjection(uv, _ShineAngle);
                float inside = step(shineLocation - _ShineWidth, projection) * step(projection, shineLocation + _ShineWidth);
                return inside * (1.0 - abs(projection - shineLocation) / _ShineWidth);
            }

            fixed4 frag(Varyings IN) : SV_Target
            {
                // Scale sprite UV inward from center, so the visible sprite can sit inset in the quad.
                float2 spriteUV = (IN.uv - 0.5) / _SpriteScale + 0.5;

                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                fixed4 tex = tex2D(_MainTex, spriteUV);

                // Masked region (e.g. the knot) keeps its plain sprite colour instead of the band tint.
                fixed3 bandColor = lerp(RainbowBand(spriteUV), fixed3(1, 1, 1), MaskAmount(spriteUV));

                // Sprite shading × band colour, then additive white shine on top.
                fixed3 rgb = tex.rgb * bandColor * IN.color.rgb;
                rgb += tex.a * ShineAmount(spriteUV);

                fixed4 result;
                result.rgb = rgb;
                result.a   = tex.a * IN.color.a * spriteMask;

                return result;
            }
            ENDCG
        }
    }
}
