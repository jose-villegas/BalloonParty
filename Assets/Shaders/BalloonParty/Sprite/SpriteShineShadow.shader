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
        [ToggleUI] _ShadowFromSceneLight ("Follow Scene Light", Float) = 0
        // Same opt-in for the shine sweep's AXIS (scenario objects); off keeps the classic
        // hardcoded 45-degree diagonal. Sweep timing is untouched either way.
        [ToggleUI] _ShineFromSceneLight ("Shine Follows Scene Light", Float) = 0
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
            #pragma target   3.5
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/ShadowBlur.cginc"
            #include "../Include/ShineSweep.cginc"
            #include "../Include/SpriteScale.cginc"
            #include "../Include/Composite.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                // Sampled ONCE per sprite (its own centre, vertex stage) so shine axis/tint
                // and shadow direction stay coherent across the whole quad — the PaintBlob
                // pattern.
                float2 lightDir   : TEXCOORD1;
                float3 lightTint  : TEXCOORD2;
                float  shadowFade : TEXCOORD3;
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

                // Sprite centre in world space (VTF, target 3.5) — one coherent light
                // reading for the whole shine/shadow instead of bending per-fragment.
                float2 spriteCenterWorld = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
                OUT.lightDir   = SceneLightDirectionAtLOD(spriteCenterWorld);
                OUT.lightTint  = SceneLightTintAtLOD(spriteCenterWorld);
                OUT.shadowFade = ShadowLightFadeAtLOD(spriteCenterWorld);
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            inline fixed SampleAlpha(float2 uv)
            {
                return SampleAlphaGuarded(_MainTex, uv);
            }

            inline fixed SoftShadowAlpha(float2 shadowUV, float s)
            {
                return SoftShadowAlpha9Tap(_MainTex, shadowUV, s);
            }

            inline fixed4 SampleSpriteWithShine(float2 uv, float2 lightDir, float3 lightTint)
            {
                fixed4 color = tex2D(_MainTex, uv);

                float shineLocation = CalcShineSweepLocation(_ShineSpeed, _ShineInterval, _ShineWidth);
                float projection = CalcShineProjection(uv, lightDir, _ShineFromSceneLight);
                fixed shineFade = CalcShineFade(projection, shineLocation, _ShineWidth);

                if (shineFade > 0)
                {
                    // Opted-in shine is "lit by the scene light" — axis AND colour — so tint it;
                    // the default (UI) sweep stays pure white regardless of the scene light.
                    float3 shineTint = _ShineFromSceneLight > 0.5 ? lightTint : float3(1.0, 1.0, 1.0);
                    color.rgb += color.a * shineFade * shineTint;
                }

                return color;
            }

            fixed4 frag(Varyings IN) : SV_Target
            {
                float2 spriteUV = ScaleSpriteUV(IN.uv, _SpriteScale);
                float  spriteMask = SpriteBoundsMask(spriteUV);

                // Main sprite with shine
                fixed4 sprite = SampleSpriteWithShine(spriteUV, IN.lightDir, IN.lightTint) * IN.color;
                sprite.a *= spriteMask;

                // Shadow — opted-in materials follow the scene light (direction away from it,
                // authored distance); the default keeps the hand-authored offset.
                float2 shadowOffset = _ShadowFromSceneLight > 0.5
                    ? -IN.lightDir * _ShadowDistance
                    : _ShadowOffset;
                float2 shadowUV = spriteUV - shadowOffset;

                fixed shadowAlpha = _ShadowSoftness < 0.0001
                    ? SampleAlpha(shadowUV)
                    : SoftShadowAlpha(shadowUV, _ShadowSoftness);

                shadowAlpha *= IN.color.a * _ShadowColor.a;
                // Only opted-in (scene-light-following) shadows fade with light intensity —
                // expressive/UI shadows stay authored regardless of the scene light.
                shadowAlpha *= _ShadowFromSceneLight > 0.5 ? IN.shadowFade : 1.0;

                fixed4 shadow = fixed4(_ShadowColor.rgb * IN.color.rgb, shadowAlpha);
                return PorterDuffOver(sprite, shadow);
            }
            ENDCG
        }
    }
}

