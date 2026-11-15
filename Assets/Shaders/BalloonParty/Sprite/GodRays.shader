// A soft fan of light rays around a point, for a SpriteRenderer quad behind something special (the golden
// reward balloon). The sprite is never sampled: the rays are worked out from the quad's local position, so
// an atlas-packed sprite (whose UVs are a sub-rect) draws the same, and the quad only needs to cover
// _RayReach. Two fans of broad cosine lobes turn slowly in opposite directions, so they drift through each
// other instead of reading as a rigid spoked wheel; a lobe's brightness also sets how far it reaches, so
// the outline is uneven. The whole thing breathes. The renderer's colour tints it, and its alpha is the
// strength. With Rainbow on (the disco ball's), each ray of the first fan takes one of the level's allowed
// colours (RainbowBand.cginc), the colours the ball turns the board into.
Shader "BalloonParty/Sprite/GodRays"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)

        [Header(Shape)]
        _RayReach("Reach (local units)", Float) = 1.4
        _ShortestRay("Shortest Ray (share of reach)", Range(0,1)) = 0.6
        _RayCount("Rays (first fan)", Range(2,24)) = 7
        _RayCount2("Rays (second fan)", Range(2,24)) = 11
        _SecondFan("Second Fan Weight", Range(0,1)) = 0.45
        _RaySharpness("Ray Sharpness", Range(1,8)) = 3
        _Falloff("Radial Falloff", Range(0.5,4)) = 1.2
        _InnerFade("Inner Fade (share of reach)", Range(0,1)) = 0.2
        _Halo("Halo Strength", Range(0,1)) = 0.35
        _HaloSize("Halo Size (share of reach)", Range(0.05,1)) = 0.45

        [Header(Colour)]
        [Toggle(_RAINBOW_RAYS)] _RainbowRays("Rainbow (the level's colours)", Float) = 0
        _RainbowSoftness("Rainbow Blend Between Rays", Range(0,1)) = 0.5

        [Header(Motion)]
        _SpinSpeed("Spin (radians per second)", Range(0,2)) = 0.12
        _BreathPeriod("Breath Period (seconds)", Range(0.5,20)) = 4
        _BreathDepth("Breath Depth", Range(0,1)) = 0.35

        [Header(Blend)]
        _Additive("Additive", Range(0,1)) = 0.6

        [MaterialToggle] PixelSnap("Pixel snap", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ _RAINBOW_RAYS
            #include "UnityCG.cginc"
            #include "../Include/MathConst.cginc"
            #include "../Include/RainbowBand.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 localPos : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;
            float _RayReach;
            float _ShortestRay;
            float _RayCount;
            float _RayCount2;
            float _SecondFan;
            float _RaySharpness;
            float _Falloff;
            float _InnerFade;
            float _Halo;
            float _HaloSize;
            float _SpinSpeed;
            float _BreathPeriod;
            float _BreathDepth;
            float _Additive;
            float _RainbowSoftness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);

                // Object space, so the fan scales and moves with its renderer's transform (the balloon's
                // spawn scale-in grows it with the balloon).
                OUT.localPos = IN.vertex.xy / max(_RayReach, 1e-4);
                OUT.color = IN.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            // One fan of soft lobes turned by `turn` radians. The count is floored to a whole number so the
            // lobes meet themselves across atan2's seam at +-pi.
            inline float Fan(float angle, float count, float turn, float offset)
            {
                float n = floor(count);
                float lobe = saturate(0.5 + 0.5 * cos(n * (angle - turn) + offset));
                return pow(lobe, _RaySharpness);
            }

            half4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float r = length(IN.localPos);
                float angle = atan2(IN.localPos.y, IN.localPos.x);
                float turn = _Time.y * _SpinSpeed;

                // The second fan turns the other way, a little slower, so the lobes shift through each other.
                float rays = lerp(Fan(angle, _RayCount, turn, 0.0),
                                  Fan(angle, _RayCount2, -0.7 * turn, 1.3),
                                  _SecondFan);

                float reach = lerp(_ShortestRay, 1.0, rays);
                float radial = pow(saturate(1.0 - r / reach), _Falloff);
                float inner = smoothstep(0.0, max(_InnerFade, 1e-4), r);
                float halo = _Halo * exp(-(r * r) / (_HaloSize * _HaloSize)) * saturate(1.0 - r);

                float breath = 1.0 - _BreathDepth * (0.5 + 0.5 * sin(_Time.y * BP_TAU / max(_BreathPeriod, 0.01)));
                float amount = saturate((rays * radial * inner + halo) * breath) * IN.color.a;

                half3 tint = IN.color.rgb;
                #ifdef _RAINBOW_RAYS
                // A lobe of the first fan peaks where n(angle - turn) is a whole turn, so this is the ray's index
                // plus a half there: each ray shows one band whole and blends into the next in the dark between.
                // A ray count that is a multiple of every band count (12) keeps the colours matching across the
                // seam at +-pi.
                float band = (angle - turn) * floor(_RayCount) / BP_TAU + 0.5;
                tint *= RainbowBandColor(band, _RainbowSoftness);
                #endif

                // Premultiplied: _Additive 1 adds light without dimming what is behind, 0 blends over it.
                half4 c;
                c.rgb = tint * amount;
                c.a = amount * (1.0 - _Additive);
                return c;
            }
            ENDCG
        }
    }
}
