Shader "BalloonParty/Balloon/RainbowBalloon"
{
    // A "rainbow star" balloon: the flat sprite tint is replaced by scrolling diagonal
    // colour bands cycling through up to four selectable colours. Colour count + values are
    // meant to be driven at runtime from the level's allowed colours (via MaterialPropertyBlock),
    // but default to the full palette so the material previews standalone. Keeps SpriteShineShadow's
    // diagonal shine sweep and soft drop shadow.

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

        [Header(Shine)]
        _ShineWidth    ("Width",    Range(0, 1))   = 0.1
        _ShineSpeed    ("Speed",    Range(0, 5))   = 1.0
        _ShineInterval ("Interval", Range(0, 10))  = 3.0

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)             = (0.2, 0.2, 0.2, 0.75)
        _ShadowOffset   ("Offset",   Vector)            = (0.025, -0.025, 0, 0)
        _ShadowSoftness ("Softness", Range(0.0, 0.1))   = 0.01

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
            fixed4    _ShadowColor;
            float2    _ShadowOffset;
            float     _ShadowSoftness;
            float     _SpriteScale;
            float     _ShineWidth;
            float     _ShineSpeed;
            float     _ShineInterval;

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

            inline fixed SampleAlpha(float2 uv)
            {
                // Out-of-bounds taps must read as transparent: with clamp wrap the sampler
                // returns the edge pixel instead, smearing streaks at the texture edge.
                float2 inBounds = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inBounds.x * inBounds.y;
            }

            inline fixed SoftShadowAlpha(float2 shadowUV, float s)
            {
                fixed a =
                    SampleAlpha(shadowUV + float2(-s, -s)) +
                    SampleAlpha(shadowUV + float2( 0, -s)) +
                    SampleAlpha(shadowUV + float2( s, -s)) +
                    SampleAlpha(shadowUV + float2(-s,  0)) +
                    SampleAlpha(shadowUV                 ) +
                    SampleAlpha(shadowUV + float2( s,  0)) +
                    SampleAlpha(shadowUV + float2(-s,  s)) +
                    SampleAlpha(shadowUV + float2( 0,  s)) +
                    SampleAlpha(shadowUV + float2( s,  s));
                return a / 9.0;
            }

            inline fixed3 ColorAt(int i)
            {
                if (i <= 0) { return _Color0.rgb; }
                if (i == 1) { return _Color1.rgb; }
                if (i == 2) { return _Color2.rgb; }
                return _Color3.rgb;
            }

            // Diagonal scrolling colour bands cycling through the first _BandCount colours.
            inline fixed3 RainbowBand(float2 uv)
            {
                float ang = _BandAngle * 6.2831853;
                float2 dir = float2(cos(ang), sin(ang));
                float projection = dot(uv - 0.5, dir) + 0.5;

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

            // Additive white shine sweep (0..1) — same diagonal band as SpriteShineShadow.
            inline fixed ShineAmount(float2 uv)
            {
                float sweepDuration = 1.0 / max(_ShineSpeed, 0.001);
                float cycleDuration = sweepDuration + _ShineInterval;
                float t = fmod(_Time.y, cycleDuration);
                float shineLocation = -_ShineWidth + (1.0 + 2.0 * _ShineWidth) * saturate(t / sweepDuration);

                float projection = (uv.x + uv.y) / 2;
                float inside = step(shineLocation - _ShineWidth, projection) * step(projection, shineLocation + _ShineWidth);
                return inside * (1.0 - abs(projection - shineLocation) / _ShineWidth);
            }

            fixed4 frag(Varyings IN) : SV_Target
            {
                // Scale sprite UV inward from center for shadow margin.
                float2 spriteUV = (IN.uv - 0.5) / _SpriteScale + 0.5;

                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                fixed4 tex = tex2D(_MainTex, spriteUV);

                // Sprite shading × band colour, then additive white shine on top.
                fixed3 rgb = tex.rgb * RainbowBand(spriteUV) * IN.color.rgb;
                rgb += tex.a * ShineAmount(spriteUV);

                fixed4 sprite;
                sprite.rgb = rgb;
                sprite.a   = tex.a * IN.color.a * spriteMask;

                // Shadow.
                float2 shadowUV = spriteUV - _ShadowOffset;
                fixed shadowAlpha = _ShadowSoftness < 0.0001
                    ? SampleAlpha(shadowUV)
                    : SoftShadowAlpha(shadowUV, _ShadowSoftness);
                shadowAlpha *= IN.color.a * _ShadowColor.a;

                // Composite shadow under sprite (Porter-Duff over).
                fixed3 shadowRGB = _ShadowColor.rgb * IN.color.rgb;
                fixed spriteA    = sprite.a;
                fixed combinedA  = spriteA + shadowAlpha * (1.0 - spriteA);

                fixed4 result;
                result.a   = combinedA;
                result.rgb = combinedA > 0.0001
                    ? (sprite.rgb * spriteA + shadowRGB * shadowAlpha * (1.0 - spriteA)) / combinedA
                    : sprite.rgb;

                return result;
            }
            ENDCG
        }
    }
}
