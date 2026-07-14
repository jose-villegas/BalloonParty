Shader "BalloonParty/Sprite/SpriteShineShadow"
{
    // Combines the diagonal shine band from ShinyDefault with the drop shadow
    // from SpriteShadow. Compatible with SpriteRenderers and Particle Systems.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Shine)]
        _ShineWidth    ("Width",    Range(0, 1))   = 0.1
        _ShineSpeed    ("Speed",    Range(0, 5))   = 1.0
        _ShineInterval ("Interval", Range(0, 10))  = 3.0

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)             = (0.2, 0.2, 0.2, 0.75)
        // OPT-IN scene lighting (see SpriteShadow.shader): off (default) keeps the authored
        // Offset; on derives direction away from _SceneLightDir at Distance (0.0354 reproduces
        // the common authored (0.025, -0.025), already on the -L axis).
        [Toggle] _ShadowFromSceneLight ("Follow Scene Light", Float) = 0
        // Same opt-in for the shine sweep's AXIS (scenario objects); off keeps the classic
        // hardcoded 45-degree diagonal. Sweep timing is untouched either way.
        [Toggle] _ShineFromSceneLight ("Shine Follows Scene Light", Float) = 0
        _ShadowOffset   ("Offset (manual)", Vector)     = (0.025, -0.025, 0, 0)
        _ShadowDistance ("Distance (scene light)", Range(0, 0.3)) = 0.0354
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
            float     _ShadowFromSceneLight;
            float     _ShineFromSceneLight;
            float2    _ShadowOffset;
            float     _ShadowDistance;
            float     _ShadowSoftness;
            float     _SpriteScale;
            float     _ShineWidth;
            float     _ShineSpeed;
            float     _ShineInterval;

            // Global shader property — set by SceneLightService, not in Properties so
            // material values can't mask it. Points TOWARD the light, normalized;
            // canonical (-0.707, 0.707) = upper-left.
            float4 _SceneLightDir;

            // Guarded read of the scene light (see SceneLightService): normalized, toward
            // the light; falls back to the canonical direction if the global hasn't been
            // pushed yet (protects edit-time before its first OnEnable/LateUpdate/OnValidate).
            float2 SceneLightDirection()
            {
                float2 raw = dot(_SceneLightDir.xy, _SceneLightDir.xy) < 1e-4
                    ? float2(-0.707, 0.707)
                    : _SceneLightDir.xy;
                return normalize(raw);
            }

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv     = IN.uv;
                OUT.color  = IN.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            inline fixed SampleAlpha(float2 uv)
            {
                // Out-of-bounds taps must read as transparent: with clamp wrap the sampler
                // returns the edge pixel instead, smearing streaks wherever opaque pixels
                // touch the texture edge.
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

            inline fixed4 SampleSpriteWithShine(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                // Sweep duration is 1/speed seconds. Full cycle = sweep + interval.
                // frac gives 0→1 within the cycle; the sweep occupies the first portion.
                float sweepDuration = 1.0 / max(_ShineSpeed, 0.001);
                float cycleDuration = sweepDuration + _ShineInterval;
                float t = fmod(_Time.y, cycleDuration);

                // Map t to shine position: -width → 1+width during sweep, then off-screen
                float shineLocation = -_ShineWidth + (1.0 + 2.0 * _ShineWidth) * saturate(t / sweepDuration);

                float lowLevel = shineLocation - _ShineWidth;
                float highLevel = shineLocation + _ShineWidth;
                // Opted-in materials sweep along the scene light's axis, travelling DOWN-light
                // (enters from the lit side); default keeps the classic hardcoded 45-degree
                // diagonal (these are mostly UI materials).
                float projection = _ShineFromSceneLight > 0.5
                    ? dot(uv - 0.5, -SceneLightDirection()) + 0.5
                    : (uv.x + uv.y) / 2;

                if (projection > lowLevel && projection < highLevel)
                {
                    float whitePower = 1 - (abs(projection - shineLocation) / _ShineWidth);
                    color.rgb += color.a * whitePower;
                }

                return color;
            }

            fixed4 frag(Varyings IN) : SV_Target
            {

                // Scale sprite UV inward from center for shadow margin
                float2 spriteUV = (IN.uv - 0.5) / _SpriteScale + 0.5;

                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                // Main sprite with shine
                fixed4 sprite = SampleSpriteWithShine(spriteUV) * IN.color;
                sprite.a *= spriteMask;

                // Shadow — opted-in materials follow the scene light (direction away from it,
                // authored distance); the default keeps the hand-authored offset. Shine sweep
                // above is untouched — pure UI/decoration, not a lighting element.
                float2 shadowOffset = _ShadowFromSceneLight > 0.5
                    ? -SceneLightDirection() * _ShadowDistance
                    : _ShadowOffset;
                float2 shadowUV = spriteUV - shadowOffset;

                fixed shadowAlpha = _ShadowSoftness < 0.0001
                    ? SampleAlpha(shadowUV)
                    : SoftShadowAlpha(shadowUV, _ShadowSoftness);

                shadowAlpha *= IN.color.a * _ShadowColor.a;

                // Composite shadow under sprite (Porter-Duff over)
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

