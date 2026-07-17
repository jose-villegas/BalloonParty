// The aim-prediction line's material: SightSmoke's drifting-noise alpha eat plus GlitterSwirl's
// orbiting speck field, combined into ONE pass so a single LineRenderer carries the whole look
// (the laser stacks two sprites for this; a line strip gets it in one draw).
//
// LineRenderer specifics: set the renderer's texture mode to TILE so uv.x is world-proportional —
// speck/smoke density then stays constant however long or bent the aim is (Stretch would make the
// pattern breathe with line length). uv.y spans 0..1 across the width, so cells-across is its own
// knob. Tint comes from the renderer's start/end colors (vertex COLOR — the config-driven trace
// colour flows in with no material coupling).
Shader "BalloonParty/Display/TraceGlitterLine"
{
    Properties
    {
        _MainTex("Line Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1, 1, 1, 1)

        [Header(Smoke Mask)]
        [NoScaleOffset] _NoiseTex("Noise (tileable)", 2D) = "white" {}
        _NoiseScale("Noise Scale", Float) = 3.0
        _ScrollSpeed("Scroll Speed (xy)", Vector) = (0.05, 0.12, 0, 0)
        _SmokeStrength("Smoke Strength", Range(0, 1)) = 0.8
        _SmokeContrast("Smoke Contrast", Range(1, 8)) = 3.0
        _MinVisibility("Min Visibility", Range(0, 1)) = 0.05

        [Header(Glitter)]
        [HDR] _GlitterColor("Speck Color", Color) = (1, 1, 1, 1)
        _GlitterDensity("Density Along Line (cells)", Range(1, 64)) = 12
        _GlitterAcross("Cells Across Width", Range(1, 8)) = 2
        _GlitterSize("Speck Size", Range(0, 0.5)) = 0.16
        _GlitterChance("Speck Chance", Range(0, 1)) = 0.35
        _GlitterSpeed("Twinkle Speed", Range(0, 20)) = 6.0
        _GlitterSharpness("Twinkle Sharpness", Range(1, 32)) = 8.0
        _GlitterBrightness("Brightness", Range(0, 3)) = 1.0

        [Header(Glitter Motion)]
        _Drift("Drift (xy dir x speed)", Vector) = (0, 0.15, 0, 0)
        _SwirlSpeed("Swirl Speed", Range(0, 20)) = 3.0
        _SwirlRadius("Swirl Radius", Range(0, 0.5)) = 0.15
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

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Include/Glitter.cginc"

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            sampler2D _NoiseTex;
            float _NoiseScale;
            float4 _ScrollSpeed;
            float _SmokeStrength;
            float _SmokeContrast;
            float _MinVisibility;
            fixed4 _GlitterColor;
            float _GlitterDensity;
            float _GlitterAcross;
            float _GlitterSize;
            float _GlitterChance;
            float _GlitterSpeed;
            float _GlitterSharpness;
            float _GlitterBrightness;
            float4 _Drift;
            float _SwirlSpeed;
            float _SwirlRadius;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // SightSmoke's mask verbatim: two offset noise samples drift past each other so patches
            // never simply repeat; contrast sharpens the wisps, the floor keeps the line readable.
            fixed SmokeMask(float2 uv)
            {
                float2 scroll = _ScrollSpeed.xy * _Time.y;
                float a = tex2D(_NoiseTex, uv * _NoiseScale + scroll).r;
                float b = tex2D(_NoiseTex, uv * _NoiseScale * 1.7 - scroll * 0.6).r;
                float n = a * b;

                float mask = saturate((n - 0.5) * _SmokeContrast + 0.5);
                mask = lerp(1.0, mask, _SmokeStrength);
                return max(mask, _MinVisibility);
            }

            // GlitterSwirl's speck field, minus the laser's mirror halves: drifting cells whose specks
            // each orbit a small circle at their own phase. Along/across densities are separate knobs
            // because uv.x tiles in world units while uv.y only spans the line's width.
            inline fixed GlitterAmount(float2 uv)
            {
                float2 cellUv = (uv + _Drift.xy * _Time.y) * float2(_GlitterDensity, _GlitterAcross);
                float2 cellId = floor(cellUv);
                float2 cellPos = frac(cellUv) - 0.5;

                float2 jitter = float2(Hash21(cellId + 17.0), Hash21(cellId + 91.0)) - 0.5;

                float  ang   = _Time.y * _SwirlSpeed + Hash21(cellId + 33.0) * BP_TAU;
                float2 orbit = float2(cos(ang), sin(ang)) * _SwirlRadius;

                float dist  = length(cellPos - (jitter * 0.6 + orbit));
                float speck = smoothstep(_GlitterSize, 0.0, dist);

                float phase   = Hash21(cellId) * BP_TAU;
                float twinkle = saturate(sin(_Time.y * _GlitterSpeed + phase) * 0.5 + 0.5);
                twinkle = pow(twinkle, max(_GlitterSharpness, 1.0));

                float active = step(1.0 - _GlitterChance, Hash21(cellId + 5.0));

                return speck * twinkle * active;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, IN.texcoord) * IN.color;
                base.a *= SmokeMask(IN.texcoord);

                // Specks confined to the visible (post-smoke) line, so sparkles ride the wisps
                // instead of floating over the eaten-away gaps.
                fixed amt = base.a * GlitterAmount(IN.texcoord);

                fixed4 c;
                c.rgb = base.rgb * base.a + _GlitterColor.rgb * (amt * _GlitterBrightness);
                c.a = saturate(base.a + _GlitterColor.a * amt);
                return c;
            }
            ENDCG
        }
    }
}
