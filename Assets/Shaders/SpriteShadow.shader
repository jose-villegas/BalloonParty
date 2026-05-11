Shader "BalloonParty/SpriteShadow"
{
    // Renders a 2D drop shadow from the sprite's alpha mask.
    // Compatible with UI Images (Canvas), SpriteRenderers, and Particle Systems.
    //
    // Shadow Offset:   X > 0 shifts shadow right, Y > 0 shifts shadow up (UV space).
    //                  Typical drop-shadow: X = 0.025, Y = -0.025 (right and down).
    // Shadow Softness: box-blur radius in UV space. 0 = hard edge.
    //                  Implemented as a 9-tap kernel.
    // Sprite Scale:    Shrinks the sprite within the quad (1 = full size, 0.8 = 80%).
    //                  Use this when the shadow is clipped by the quad edge — scaling
    //                  down creates transparent margins where the shadow can render freely.

    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)             = (0.2, 0.2, 0.2, 0.75)
        _ShadowOffset   ("Offset",   Vector)            = (0.025, -0.025, 0, 0)
        _ShadowSoftness ("Softness", Range(0.0, 0.1))   = 0.01

        [Header(Sprite)]
        _SpriteScale ("Scale",  Range(0.1, 1.0)) = 1.0

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
            #pragma target   2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile __ UNITY_UI_CLIP_RECT
            #pragma multi_compile __ UNITY_UI_ALPHACLIP

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
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            fixed4    _Color;
            fixed4    _ShadowColor;
            float2    _ShadowOffset;
            float     _ShadowSoftness;
            float     _SpriteScale;
            float4    _ClipRect;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = IN.vertex;
                OUT.vertex        = UnityObjectToClipPos(IN.vertex);
                OUT.uv            = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color         = IN.color * _Color;
                return OUT;
            }

            inline fixed SampleAlpha(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
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

            fixed4 frag(Varyings IN) : SV_Target
            {
                // Scale the sprite UV inward from center so the quad has transparent
                // margins on all sides — shadow can bleed into those margins freely.
                float2 spriteUV = (IN.uv - 0.5) / _SpriteScale + 0.5;

                // Anything outside [0,1] after scaling is beyond the sprite — transparent.
                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                // Main sprite colour — masked to bounds
                fixed4 sprite  = tex2D(_MainTex, spriteUV) * IN.color;
                sprite.a      *= spriteMask;

                // Shadow samples from the same scaled UV, then shifted.
                // Subtracting the offset means the shadow appears at +offset in screen space.
                float2 shadowUV = spriteUV - _ShadowOffset;

                fixed shadowAlpha = _ShadowSoftness < 0.0001
                    ? SampleAlpha(shadowUV)
                    : SoftShadowAlpha(shadowUV, _ShadowSoftness);

                // Weight by vertex alpha and shadow colour alpha
                shadowAlpha *= IN.color.a * _ShadowColor.a;

                // Porter-Duff "over": composite shadow under sprite
                //   A_out   = A_sprite + A_shadow * (1 - A_sprite)
                //   RGB_out = (RGB_sprite * A_sprite + RGB_shadow * A_shadow * (1 - A_sprite)) / A_out
                // Shadow RGB is tinted by the renderer's vertex color, mirroring how the sprite is tinted.
                fixed3 shadowRGB = _ShadowColor.rgb * IN.color.rgb;
                fixed spriteA   = sprite.a;
                fixed combinedA = spriteA + shadowAlpha * (1.0 - spriteA);

                fixed4 result;
                result.a   = combinedA;
                result.rgb = combinedA > 0.0001
                    ? (sprite.rgb * spriteA + shadowRGB * shadowAlpha * (1.0 - spriteA)) / combinedA
                    : sprite.rgb;

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
