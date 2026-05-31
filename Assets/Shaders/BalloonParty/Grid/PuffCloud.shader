Shader "BalloonParty/Grid/PuffCloud"
{
    // ── Phase P1 — Cloud shader prototype ──────────────────────────────────
    // Procedural cloud on a SpriteRenderer quad (no assigned sprite).
    // Three-octave Simplex noise sampled in world space for spatial stability.
    // Slot-center array drives boundary falloff and occupancy masking — works
    // identically for single-slot (P1, 1 entry) and merged clusters (P3, N entries).
    //
    // GPU instancing DISABLED — per-instance _TimeOffset driven via MaterialPropertyBlock.
    //
    // Reference dimensions: SlotSeparation = (1.0, 0.85).
    // ───────────────────────────────────────────────────────────────────────
    Properties
    {
        _Color              ("Tint",               Color)              = (0.85, 0.95, 1.0, 1.0)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Noise)]
        _NoiseScale         ("Global Noise Scale", Float)              = 1.0
        _BaseScale          ("Base Octave Scale",  Float)              = 2.0
        _DetailScale        ("Detail Octave Scale",Float)              = 5.0
        _FineScale          ("Fine Octave Scale",  Float)              = 10.0
        _ScrollSpeedBase    ("Scroll Speed Base",  Vector)             = (0.03, 0.02, 0, 0)
        _ScrollSpeedDetail  ("Scroll Speed Detail",Vector)             = (0.06, -0.04, 0, 0)
        _ScrollSpeedFine    ("Scroll Speed Fine",  Vector)             = (-0.04, 0.08, 0, 0)
        _EdgeLow            ("Edge Low Threshold", Range(0, 1))        = 0.35
        _EdgeHigh           ("Edge High Threshold",Range(0, 1))        = 0.55

        [Header(Visual)]
        _CloudColor         ("Cloud Color",        Color)              = (1, 1, 1, 0.6)
        _SpriteScale        ("Sprite Scale",       Range(0.3, 1.0))    = 0.70
        _BorderSoftness     ("Border Softness",    Range(0, 0.5))      = 0.15
        _SlotRadius         ("Slot Radius",        Float)              = 0.45

        [Header(Density)]
        [Toggle(_DENSITY_ON)] _EnableDensity ("Enable Density RT", Float) = 0
        _DensityTex         ("Density Texture",    2D)                 = "white" {}

        [Header(Animation)]
        _TimeOffset         ("Time Offset",        Float)              = 0.0

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow",   Float)  = 0
        _ShadowColor        ("Shadow Color",       Color)              = (0.06, 0.06, 0.14, 0.35)
        _ShadowOffsetX      ("Shadow Offset X",    Range(-0.15, 0.15)) = 0.025
        _ShadowOffsetY      ("Shadow Offset Y",    Range(-0.15, 0.15)) = -0.030
        _ShadowSoftness     ("Shadow Softness",    Range(0, 0.10))     = 0.03
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
            #pragma shader_feature _DENSITY_ON
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "Assets/Shaders/BalloonParty/Noise/SimplexNoise2D.cginc"

            // Max slot centers for merged clusters (P3); P1 uses 1.
            #define MAX_SLOTS 16

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
                float2 worldPos : TEXCOORD1;
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
            float  _NoiseScale;
            float  _BaseScale;
            float  _DetailScale;
            float  _FineScale;
            float4 _ScrollSpeedBase;
            float4 _ScrollSpeedDetail;
            float4 _ScrollSpeedFine;
            float  _EdgeLow;
            float  _EdgeHigh;
            fixed4 _CloudColor;
            float  _SpriteScale;
            float  _BorderSoftness;
            float  _SlotRadius;
            float  _TimeOffset;

            #ifdef _DENSITY_ON
            sampler2D _DensityTex;
            #endif

            // Slot center positions in world space — set via MaterialPropertyBlock.
            float4 _SlotCentersWorld[MAX_SLOTS];
            int    _SlotCount;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowOffsetX;
            float  _ShadowOffsetY;
            float  _ShadowSoftness;
            #endif

            // Three-octave Simplex noise blend.
            // Weights: base 0.50, detail 0.30, fine 0.20.
            // Returns [0, 1] (remapped from raw [-1, 1] per octave).
            float CloudNoise(float2 wp, float t)
            {
                float2 pBase   = wp * _BaseScale   * _NoiseScale + _ScrollSpeedBase.xy   * t;
                float2 pDetail = wp * _DetailScale  * _NoiseScale + _ScrollSpeedDetail.xy * t;
                float2 pFine   = wp * _FineScale    * _NoiseScale + _ScrollSpeedFine.xy   * t;

                float n  = SimplexNoise2D(pBase)   * 0.50;
                n       += SimplexNoise2D(pDetail) * 0.30;
                n       += SimplexNoise2D(pFine)   * 0.20;

                return n * 0.5 + 0.5;
            }

            // Distance-to-nearest-slot-center falloff.
            // Returns 0 far from any slot, 1 at/near a slot center.
            float SlotFalloff(float2 wp)
            {
                float minDist = 999.0;
                for (int i = 0; i < _SlotCount; i++)
                {
                    minDist = min(minDist, length(wp - _SlotCentersWorld[i].xy));
                }
                return smoothstep(_SlotRadius + _BorderSoftness, _SlotRadius, minDist);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;

                float4 worldVert = mul(unity_ObjectToWorld, IN.vertex);
                OUT.worldPos = worldVert.xy;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                float2 wp = IN.worldPos;
                float  t  = _TimeOffset;

                // Noise-based cloud shape
                float noiseValue = CloudNoise(wp, t);
                float cloud = smoothstep(_EdgeLow, _EdgeHigh, noiseValue);

                // Density field masking (P2+)
                #ifdef _DENSITY_ON
                float density = tex2D(_DensityTex, IN.texcoord).r;
                cloud *= density;
                #endif

                // Boundary falloff — occupancy mask via slot centers
                float borderFade = SlotFalloff(wp);
                cloud *= borderFade;

                // Early discard fully transparent pixels
                #ifdef _SHADOW_ON
                // Compute shadow before discarding so shadow-only pixels survive
                float2 shadowWp = wp - float2(_ShadowOffsetX, _ShadowOffsetY);
                float  shadowNoise = CloudNoise(shadowWp, t);
                float  shadowCloud = smoothstep(_EdgeLow, _EdgeHigh, shadowNoise);
                #ifdef _DENSITY_ON
                shadowCloud *= tex2D(_DensityTex, IN.texcoord).r;
                #endif
                float  shadowFade  = SlotFalloff(shadowWp);
                shadowCloud *= shadowFade;

                float shadowAlpha = shadowCloud * _ShadowColor.a * IN.color.a;
                shadowAlpha *= smoothstep(0.0, _ShadowSoftness + 0.01, shadowCloud);

                if (cloud < 0.001 && shadowAlpha < 0.001) discard;

                // Pure shadow pixel — main cloud absent
                if (cloud < 0.001)
                {
                    return fixed4(_ShadowColor.rgb, shadowAlpha);
                }

                // Compose main cloud with shadow behind
                float mainAlpha = cloud * _CloudColor.a * IN.color.a;
                fixed3 mainRgb  = _CloudColor.rgb * IN.color.rgb;

                fixed  combinedA   = mainAlpha + shadowAlpha * (1.0 - mainAlpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (mainRgb * mainAlpha + _ShadowColor.rgb * shadowAlpha * (1.0 - mainAlpha)) / combinedA
                    : mainRgb;

                return fixed4(combinedRGB, combinedA);
                #else
                if (cloud < 0.001) discard;

                float mainAlpha = cloud * _CloudColor.a * IN.color.a;
                fixed3 mainRgb  = _CloudColor.rgb * IN.color.rgb;

                return fixed4(mainRgb, mainAlpha);
                #endif
            }
            ENDCG
        }
    }
}

