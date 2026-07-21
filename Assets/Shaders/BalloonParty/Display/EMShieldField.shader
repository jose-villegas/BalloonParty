// Procedural electromagnetic field lines wrapping around the projectile in a dipole pattern.
// Each shield layer is a concentric shell; dissolve sweeps apex-first via directional noise.
// Driven by MaterialPropertyBlock: _DissolveProgress[5], _Color, _ActiveLayers.
Shader "BalloonParty/Display/EMShieldField"
{
    Properties
    {
        _MainTex("Sprite (alpha mask)", 2D) = "white" {}
        [HDR] _Color("Field Tint", Color) = (0.5, 0.8, 1, 1)

        [Header(Geometry)]
        _BaseRadius("Base Shell Radius", Range(0.1, 0.5)) = 0.2
        _LayerSpacing("Layer Spacing", Range(0.02, 0.15)) = 0.06
        _ActiveLayers("Active Layers", Range(0, 5)) = 3

        [Header(Line Appearance)]
        _FieldLineThickness("Line Thickness", Range(0.002, 0.05)) = 0.015
        _GlowWidth("Glow Width", Range(0.01, 0.2)) = 0.06
        _GlowIntensity("Glow Intensity", Range(0, 3)) = 1.0
        _PulseSpeed("Pulse Speed", Range(0, 10)) = 3.0

        [Header(Dissolve)]
        _NoiseScale("Noise Scale", Range(1, 20)) = 8.0
        _DirectionalBias("Direction Bias", Range(0, 1)) = 0.6
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
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            #define EM_PI  3.14159265359
            #define EM_TAU 6.28318530718
            #define EM_MAX_LAYERS 5

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

            float _BaseRadius;
            float _LayerSpacing;
            float _ActiveLayers;
            float _FieldLineThickness;
            float _GlowWidth;
            float _GlowIntensity;
            float _PulseSpeed;
            float _NoiseScale;
            float _DirectionalBias;

            // Per-layer dissolve progress [0..1], pushed via MaterialPropertyBlock.SetFloatArray.
            float _DissolveProgress[EM_MAX_LAYERS];

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Cheap hash-based value noise (no texture dependency).
            inline float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            inline float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                f = f * f * (3.0 - 2.0 * f); // smoothstep interpolant

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Evaluate the signed distance from a pixel to the dipole field line at shell radius R.
            // In polar coords: r_line = R * sin²(theta). We measure the pixel's radial offset from
            // where the field line should be at this theta.
            inline float FieldLineSDF(float2 polarUV, float shellRadius)
            {
                float theta = polarUV.y; // [0, PI] mapped from V
                float sinTheta = sin(theta);
                float expectedR = shellRadius * sinTheta * sinTheta;
                return abs(polarUV.x - expectedR);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Remap UV to polar-like coordinates centered on the projectile.
                // U: lateral distance from center axis (0 = axis, 0.5 = edge)
                // V: pole angle theta mapped to [0, PI]
                float2 uv = IN.texcoord;
                float lateralDist = abs(uv.x - 0.5); // distance from center axis
                float theta = uv.y * EM_PI;          // V=0 → theta=0 (south pole), V=1 → theta=PI (north/apex)

                float2 polarUV = float2(lateralDist, theta);

                float totalCore = 0.0;
                float totalGlow = 0.0;
                int layers = (int)clamp(_ActiveLayers, 0, EM_MAX_LAYERS);

                // Dissolve noise: computed once, biased per-layer by directional term.
                float noiseVal = ValueNoise(uv * _NoiseScale + _Time.y * 0.3);

                // Directional bias: apex (V=1, theta=PI) dissolves first → bias = (1 - V).
                float dirBias = (1.0 - uv.y) * _DirectionalBias;

                [unroll(EM_MAX_LAYERS)]
                for (int i = 0; i < EM_MAX_LAYERS; i++)
                {
                    if (i >= layers)
                    {
                        break;
                    }

                    float shellR = _BaseRadius + i * _LayerSpacing;
                    float dist = FieldLineSDF(polarUV, shellR);

                    // Dissolve: threshold = noise + directional bias. If progress > threshold, clip.
                    float dissolveThreshold = noiseVal + dirBias;
                    float dissolve = step(dissolveThreshold, _DissolveProgress[i]);
                    if (dissolve > 0.5)
                    {
                        continue;
                    }

                    // Pulse: per-layer phase offset for visual richness.
                    float pulse = 0.85 + 0.15 * sin(_Time.y * _PulseSpeed + i * 1.2);

                    // Pole fade: avoid singularity where all lines converge (sin²(θ) → 0).
                    float sinTheta2 = sin(theta);
                    sinTheta2 *= sinTheta2;
                    float poleFade = smoothstep(0.0, 0.15, sinTheta2);

                    // Core strand.
                    float thickness = _FieldLineThickness * pulse;
                    float core = smoothstep(thickness, thickness * 0.3, dist) * poleFade;

                    // Glow halo.
                    float glow = exp(-dist / max(_GlowWidth, 1e-4)) * poleFade * pulse;

                    // Dissolve edge glow: brighten near the dissolve frontier.
                    float dissolveDist = saturate(1.0 - abs(_DissolveProgress[i] - dissolveThreshold) * 4.0);
                    glow += dissolveDist * 0.5 * poleFade;

                    totalCore = max(totalCore, core);
                    totalGlow += glow;
                }

                // Early out if nothing visible.
                float intensity = totalCore + totalGlow * _GlowIntensity * 0.25;
                if (intensity < 0.001)
                {
                    discard;
                }

                // Premultiplied alpha output.
                fixed4 c;
                c.rgb = IN.color.rgb * intensity * IN.color.a;
                c.a = saturate(totalCore * IN.color.a);
                return c;
            }
            ENDCG
        }
    }
}
