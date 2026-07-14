Shader "BalloonParty/Sprite/SpriteShadowComposite"
{
    // Extends SpriteShadow with a second sprite layer composited on top of the first.
    //
    // Layer order (bottom → top):
    //   1. Drop shadow  (from MainTex alpha, same as SpriteShadow)
    //   2. First sprite (_MainTex), tinted by _FirstLayerColor
    //   3. Second sprite (_SecondTex), tinted by _SecondLayerColor
    //
    // The global Tint (_Color) is multiplied into BOTH sprite layers (and the vertex color),
    // so you can still fade / tint the whole object from the renderer.
    //
    // Shadow direction is OPT-IN scene lighting per material (Follow Scene Light): on, the
    // shadow sits at Shadow Distance away from _SceneLightDir; off (default), the authored
    // Shadow Offset applies — expressive shadows stay art.
    // Shadow Softness: box-blur radius in UV space. 0 = hard edge.
    //                  Implemented as a 9-tap kernel.
    // Sprite Scale:    Shrinks both sprites within the quad (1 = full size, 0.8 = 80%).
    //                  Use this when the shadow is clipped by the quad edge.

    Properties
    {
        _MainTex ("Sprite Texture (Layer 1)", 2D) = "white" {}
        _Color   ("Tint (both layers)",       Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(First Layer)]
        _FirstLayerColor ("Color", Color) = (1, 1, 1, 1)

        [Header(Second Layer)]
        _SecondTex        ("Sprite Texture (Layer 2)", 2D)    = "white" {}
        _SecondLayerColor ("Color",                    Color) = (1, 1, 1, 1)

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)             = (0.2, 0.2, 0.2, 0.75)
        // OPT-IN scene lighting (see SpriteShadow.shader): off (default) keeps the authored
        // Offset; on derives direction away from _SceneLightDir at Distance (0.1414 reproduces
        // the sole authored material's (0.1, -0.1), already on the -L axis).
        [ToggleUI] _ShadowFromSceneLight ("Follow Scene Light", Float) = 0
        _ShadowOffset   ("Offset (manual)", Vector)    = (0.025, -0.025, 0, 0)
        _ShadowDistance ("Distance (scene light)", Range(0, 1)) = 0.1414
        _ShadowSoftness ("Softness", Range(0.0, 0.1))  = 0.01

        [Header(Sprite)]
        _SpriteScale ("Scale", Range(0.1, 1.0)) = 1.0

        [Header(Rendering)]
        [Enum(UnityEngine.Rendering.CompareFunction)]
        _ZTest ("Z Test", Float) = 4

        [Header(UI Stencil)]
        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",         Float) = 0
        _StencilOp        ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask        ("Color Mask",         Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
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

        Stencil
        {
            Ref       [_Stencil]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
            CompFront [_StencilComp]
            PassFront [_StencilOp]
            FailFront Keep
            ZFailFront Keep
            CompBack  Always
            PassBack  Keep
            FailBack  Keep
            ZFailBack Keep
        }

        Cull      Off
        Lighting  Off
        ZWrite    Off
        ZTest     [_ZTest]
        Blend     SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.5

            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                // Sampled ONCE per sprite (its own centre, vertex stage) so the shadow direction
                // stays coherent across the whole quad — the PaintBlob pattern.
                float2 lightDir      : TEXCOORD2;
                float  shadowFade    : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            sampler2D _SecondTex;
            fixed4    _FirstLayerColor;
            fixed4    _SecondLayerColor;
            fixed4    _ShadowColor;
            float     _ShadowFromSceneLight;
            float2    _ShadowOffset;
            float     _ShadowDistance;
            float     _ShadowSoftness;
            float     _SpriteScale;
            float4    _ClipRect;

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
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = IN.vertex;
                OUT.vertex        = UnityObjectToClipPos(IN.vertex);
                OUT.uv            = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color         = IN.color * _Color * _RendererColor;

                // Sprite centre in world space (VTF, target 3.5) — one coherent light
                // reading for the whole shadow instead of bending per-fragment.
                float2 spriteCenterWorld = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
                OUT.lightDir   = SceneLightDirectionAtLOD(spriteCenterWorld);
                OUT.shadowFade = ShadowLightFadeAtLOD(spriteCenterWorld);
                return OUT;
            }

            // ------------------------------------------------------------------ helpers

            inline fixed SampleAlpha(float2 uv)
            {
                // Out-of-bounds taps must read as transparent: with clamp wrap the sampler
                // returns the edge pixel instead, smearing streaks wherever opaque pixels
                // touch the texture edge.
                float2 inBounds = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inBounds.x * inBounds.y;
            }

            // 9-tap box blur centred on shadowUV.
            // When softness == 0 all taps collapse to the same point (hard edge).
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

            // Porter-Duff "over": composite src on top of dst.
            // Returns pre-multiplied (rgb*a, a) form for chaining.
            inline fixed4 Over(fixed4 src, fixed4 dst)
            {
                fixed4 result;
                result.a   = src.a + dst.a * (1.0 - src.a);
                result.rgb = result.a > 0.0001
                    ? (src.rgb * src.a + dst.rgb * dst.a * (1.0 - src.a)) / result.a
                    : dst.rgb;
                return result;
            }

            // ------------------------------------------------------------------ fragment

            fixed4 frag(Varyings IN) : SV_Target
            {
                // Scale both sprites inward from center so the quad has transparent
                // margins on all sides — shadow can bleed into those margins freely.
                float2 spriteUV = (IN.uv - 0.5) / _SpriteScale + 0.5;

                // Anything outside [0,1] after scaling is beyond the sprite — transparent.
                float2 inBounds  = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                // ---- Layer 1: main sprite ----------------------------------------
                fixed4 layer1  = tex2D(_MainTex, spriteUV) * _FirstLayerColor * IN.color;
                layer1.a      *= spriteMask;

                // ---- Layer 2: second sprite (tinted by _SecondLayerColor × global tint)
                // _SecondLayerColor is an additional per-layer tint; the global tint
                // (_Color, baked into IN.color) is also applied so both layers respond
                // to the renderer's overall tint / alpha uniformly.
                fixed4 layer2  = tex2D(_SecondTex, spriteUV) * _SecondLayerColor * IN.color;
                layer2.a      *= spriteMask;

                // ---- Shadow ------------------------------------------------------
                // Opted-in materials follow the scene light (direction away from it, authored
                // distance); the default keeps the hand-authored offset. Applied in local
                // sprite UV — no vertex world-rotation capture (accepted; sway is small).
                float2 shadowOffset = _ShadowFromSceneLight > 0.5
                    ? -IN.lightDir * _ShadowDistance
                    : _ShadowOffset;
                float2 shadowUV = spriteUV - shadowOffset;

                fixed shadowAlpha = _ShadowSoftness < 0.0001
                    ? SampleAlpha(shadowUV)
                    : SoftShadowAlpha(shadowUV, _ShadowSoftness);

                // Weight by vertex alpha and shadow colour alpha
                shadowAlpha *= IN.color.a * _ShadowColor.a;
                // Only opted-in (scene-light-following) shadows fade with light intensity —
                // expressive/UI shadows stay authored regardless of the scene light.
                shadowAlpha *= _ShadowFromSceneLight > 0.5 ? IN.shadowFade : 1.0;

                // Shadow RGB tinted by the global tint (mirrors the sprite behaviour)
                fixed4 shadow;
                shadow.rgb = _ShadowColor.rgb * IN.color.rgb;
                shadow.a   = shadowAlpha;

                // ---- Composite: shadow → layer1 → layer2 (bottom → top) ----------
                fixed4 result = shadow;
                result        = Over(layer1, result); // layer1 over shadow
                result        = Over(layer2, result); // layer2 over (layer1+shadow)

                #ifdef UNITY_UI_CLIP_RECT
                result.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(result.a - 0.001);
                #endif

                return result;
            }
            ENDCG
        }
    }
}





