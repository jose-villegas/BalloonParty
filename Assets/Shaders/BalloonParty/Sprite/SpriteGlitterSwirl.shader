// Renders ONLY the twinkling glitter — the assigned sprite is never drawn, its alpha just masks where
// specks may appear (so a shaped sprite confines the sparkle). Same grid-hash sparkle as Sprite/Glitter,
// in the standard premultiplied sprite pass, plus motion: the field drifts toward _Drift and each speck
// orbits a small circle (_SwirlSpeed/_SwirlRadius) so it reads as spinning around, e.g., a laser beam.
Shader "BalloonParty/Sprite/GlitterSwirl"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)

        [Header(Glitter)]
        [HDR] _GlitterColor("Speck Color", Color) = (1,1,1,1)
        _GlitterDensity("Density (cells)", Range(4,64)) = 24
        _GlitterSize("Speck Size", Range(0,0.5)) = 0.16
        _GlitterChance("Speck Chance", Range(0,1)) = 0.35
        _GlitterSpeed("Twinkle Speed", Range(0,20)) = 6.0
        _GlitterSharpness("Twinkle Sharpness", Range(1,32)) = 8.0
        _GlitterBrightness("Brightness", Range(0,3)) = 1.0

        [Header(Motion)]
        _Drift("Drift (xy dir x speed)", Vector) = (0, 0.15, 0, 0)
        _SwirlSpeed("Swirl Speed", Range(0,20)) = 3.0
        _SwirlRadius("Swirl Radius", Range(0,0.5)) = 0.15

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
            #include "UnityCG.cginc"

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
                float2 texcoord : TEXCOORD0;
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

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _AlphaSplitEnabled;
            fixed4 _GlitterColor;
            float _GlitterDensity;
            float _GlitterSize;
            float _GlitterChance;
            float _GlitterSpeed;
            float _GlitterSharpness;
            float _GlitterBrightness;
            float4 _Drift;
            float _SwirlSpeed;
            float _SwirlRadius;

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                #if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
                if (_AlphaSplitEnabled)
                    color.a = tex2D(_AlphaTex, uv).r;
                #endif

                return color;
            }

            // Cheap deterministic 2D hash -> pseudo-random value in [0, 1). No texture lookup needed.
            inline float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Scattered twinkling specks: tile UV into a grid, jitter each speck off its cell centre,
            // only some cells sparkle at all, and each blinks at its own random phase/speed.
            inline fixed GlitterAmount(float2 uv)
            {
                // Drift the whole field toward _Drift so glints stream in a direction.
                float2 cellUv  = (uv + _Drift.xy * _Time.y) * _GlitterDensity;
                float2 cellId  = floor(cellUv);
                float2 cellPos = frac(cellUv) - 0.5;

                float2 jitter = float2(Hash21(cellId + 17.0), Hash21(cellId + 91.0)) - 0.5;

                // Each speck orbits a small circle at its own phase — reads as spinning around the beam
                // rather than a rigid slide.
                float  ang    = _Time.y * _SwirlSpeed + Hash21(cellId + 33.0) * 6.2831853;
                float2 orbit  = float2(cos(ang), sin(ang)) * _SwirlRadius;

                float  dist   = length(cellPos - (jitter * 0.6 + orbit));
                float  speck  = smoothstep(_GlitterSize, 0.0, dist);

                float rnd     = Hash21(cellId);
                float phase   = rnd * 6.2831853;
                float twinkle = saturate(sin(_Time.y * _GlitterSpeed + phase) * 0.5 + 0.5);
                twinkle = pow(twinkle, max(_GlitterSharpness, 1.0));

                float active  = step(1.0 - _GlitterChance, Hash21(cellId + 5.0));

                return speck * twinkle * active;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // The sprite is never drawn — only its alpha (× renderer alpha) confines the sparkle.
                fixed mask = SampleSpriteTexture(IN.texcoord).a * IN.color.a;
                fixed amt = mask * GlitterAmount(IN.texcoord);

                fixed4 c;
                c.rgb = _GlitterColor.rgb * (amt * _GlitterBrightness);
                c.a = _GlitterColor.a * amt;
                return c;
            }
            ENDCG
        }
    }
}
