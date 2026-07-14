Shader "BalloonParty/Grid/BushLeaf"
{
    Properties
    {
        _MainTex ("Leaf Atlas", 2D) = "white" {}
        _LeafColor ("Leaf Tint", Color) = (1, 1, 1, 1)

        [Header(Shadow)]
        _ShadowColor    ("Color",    Color)           = (0.15, 0.18, 0.1, 0.55)
        // Direction now derives from the scene light (see SceneLightDirection below); this is
        // just the distance along that direction. Default reproduces LeafMain.mat's measured
        // (0.1, -0.1) authored offset (|.| = 0.1414), which already sits on the -L axis.
        _ShadowDistance ("Distance", Range(0, 0.3))   = 0.1414
        _ShadowSoftness ("Softness", Range(0, 0.08))  = 0.015

        [Header(Highlight)]
        _HighlightColor    ("Color",     Color)          = (1, 1, 0.9, 0.4)
        // Direction derives from the scene light too (+L: highlights face the light, shadows
        // face away from it). Default reproduces LeafMain.mat's measured (-0.08, 0.08) authored
        // offset (|.| = 0.113).
        _HighlightDistance ("Distance",  Range(0, 0.3))  = 0.113
        _HighlightSize     ("Size",      Range(0.01, 0.3)) = 0.1
        _HighlightSoftness ("Softness",  Range(0.01, 0.3)) = 0.12

        [Header(Light Response)]
        // Diffuse response to the scene light (colour x intensity, like Sprite/Diffuse):
        // 0 = unlit (authored look always), 1 = fully lit.
        _LightInfluence ("Light Influence", Range(0, 1)) = 1

        [Header(Sprite)]
        _SpriteScale ("Scale", Range(0.3, 1.0)) = 0.75

        [Header(Wind)]
        _WindFrequency      ("Frequency",       Float)              = 0.5
        _WindAmplitude      ("Amplitude (deg)",  Float)              = 3.0
        _WindNoiseAmplitude ("Noise Amp (deg)",  Float)              = 1.5
        _WindScalePulse     ("Scale Pulse",      Range(0, 0.1))     = 0.03
        _PivotOffset        ("Pivot Offset",     Range(-0.5, 0.5))  = 0.0

        [Header(Rattle)]
        [Toggle(_RATTLE_ON)] _EnableRattle ("Enable Rattle", Float) = 0
        _RattleAmplitude    ("Amplitude (deg)", Float)              = 15.0
        _RattleFrequency    ("Oscillation Freq", Float)             = 12.0
        _RattleDamping      ("Damping",    Range(1, 10))            = 3.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _RATTLE_ON
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            fixed4 _LeafColor;
            fixed4 _ShadowColor;
            float  _ShadowDistance;
            float  _ShadowSoftness;
            fixed4 _HighlightColor;
            float  _HighlightDistance;
            float  _HighlightSize;
            float  _HighlightSoftness;
            float     _LightInfluence;
            float  _SpriteScale;
            float  _WindFrequency;
            float  _WindAmplitude;
            float  _WindNoiseAmplitude;
            float  _WindScalePulse;
            float  _PivotOffset;

            // Global shader property — set by SceneLightService, not in Properties so
            // material values can't mask it. Points TOWARD the light, normalized;
            // canonical (-0.707, 0.707) = upper-left.
            float4 _SceneLightDir;

            // Set globally by SceneLightService; kept out of Properties so no
            // material value can shadow the scene-wide light. Colour's alpha is the
            // "owner has pushed" validity flag (see SceneLightTint).
            float4 _SceneLightColor;
            float  _SceneLightIntensity;

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

            // The light's colour × intensity — multiplies into the authored specular response.
            // Neutral (white) when the owner hasn't pushed yet, so nothing dims at edit time.
            float3 SceneLightTint()
            {
                return _SceneLightColor.a > 0.5
                    ? _SceneLightColor.rgb * _SceneLightIntensity
                    : float3(1.0, 1.0, 1.0);
            }

            #ifdef _RATTLE_ON
            sampler2D _DisturbanceTex;
            float2    _FieldBoundsMin;
            float2    _FieldBoundsSize;
            float     _RattleAmplitude;
            float     _RattleFrequency;
            float     _RattleDamping;
            #endif

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _LeafTint)
                UNITY_DEFINE_INSTANCED_PROP(float4, _UVRect)
                UNITY_DEFINE_INSTANCED_PROP(float4, _LeafWind)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 rawUV : TEXCOORD1;
                float4 uvRect : TEXCOORD2;
                float2 localShadowOffset : TEXCOORD3;
                float2 localHighlightOffset : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                float4 rect = UNITY_ACCESS_INSTANCED_PROP(Props, _UVRect);
                o.rawUV = v.uv;
                o.uvRect = rect;

                // Per-instance wind data: x=phase, y=depth, z=baseAngle (rad), w=scale
                float4 wind = UNITY_ACCESS_INSTANCED_PROP(Props, _LeafWind);
                float phase = wind.x;
                float depth = wind.y;
                float baseAngle = wind.z;
                float leafScale = wind.w;

                float t = _Time.y;

                // Wind rotation: sine + dual-sine organic noise approximation
                float sineRot = sin(t * _WindFrequency + phase) * _WindAmplitude * depth;
                float noise = sin(t * 0.3 + phase * 17.3) * sin(t * 0.7 + phase * 31.1)
                            * _WindNoiseAmplitude * depth;
                float windDeg = sineRot + noise;

                // Rattle: sample disturbance field at leaf world position
                float rattleDeg = 0.0;
                #ifdef _RATTLE_ON
                {
                    float2 leafWorld = mul(UNITY_MATRIX_M, float4(0.0, 0.0, 0.0, 1.0)).xy;
                    float2 fieldUV = (leafWorld - _FieldBoundsMin) / _FieldBoundsSize;
                    float3 field = tex2Dlod(_DisturbanceTex, float4(fieldUV, 0.0, 0.0)).rgb;
                    float2 displace = (field.gb - 0.5) * 2.0;
                    float disturbance = length(displace);

                    // Power curve damping: low displacement decays faster
                    disturbance = pow(disturbance, _RattleDamping);

                    // Convert displacement to angular rattle with fast oscillation
                    // Cross product with leaf direction gives signed rotation
                    float2 leafDir = float2(cos(baseAngle), sin(baseAngle));
                    float cross = displace.x * leafDir.y - displace.y * leafDir.x;
                    float rattleSign = sign(cross + 0.0001);

                    rattleDeg = disturbance * _RattleAmplitude * depth * rattleSign
                              * (0.5 + 0.5 * sin(t * _RattleFrequency + phase * 3.7));
                }
                #endif

                // Scale pulse
                float scale = leafScale;
                if (_WindScalePulse > 0.001)
                {
                    scale *= 1.0 + sin(t * _WindFrequency * 2.0 + phase) * _WindScalePulse * depth;
                }

                // Total rotation in radians (wind + rattle in degrees → radians)
                float totalAngle = baseAngle - 1.5707963 + (windDeg + rattleDeg) * 0.0174533;
                float cosA = cos(totalAngle);
                float sinA = sin(totalAngle);

                // Pivot-based rotation in local quad space
                // Quad is bottom-pivoted (y: 0→1), pivot at y = pivotOffset + 0.5
                float2 pivot = float2(0.0, _PivotOffset + 0.5);
                float2 local = v.vertex.xy - pivot;
                local *= scale;
                float2 rotated = float2(
                    cosA * local.x - sinA * local.y,
                    sinA * local.x + cosA * local.y);

                // Matrix encodes world position only (translation, no rotation/scale)
                float4 worldPos = mul(UNITY_MATRIX_M, float4(rotated, 0.0, 1.0));
                o.pos = mul(UNITY_MATRIX_VP, worldPos);

                // Scale sprite inward from center to create margins for shadow
                float2 spriteUV = (v.uv - 0.5) / _SpriteScale + 0.5;
                o.uv = rect.xy + spriteUV * rect.zw;

                // Shadow lands away from the scene light, highlight faces toward it — only the
                // distance stays authored, so rotating the light moves both together.
                float2 shadowOffset = -SceneLightDirection() * _ShadowDistance;
                float2 highlightOffset = SceneLightDirection() * _HighlightDistance;

                // Inverse-rotate shadow/highlight offsets using the animated rotation — this is
                // what keeps both world-anchored under wind/rattle instead of spinning with the leaf.
                o.localShadowOffset = -float2(
                     cosA * shadowOffset.x + sinA * shadowOffset.y,
                    -sinA * shadowOffset.x + cosA * shadowOffset.y);

                o.localHighlightOffset = -float2(
                     cosA * highlightOffset.x + sinA * highlightOffset.y,
                    -sinA * highlightOffset.x + cosA * highlightOffset.y);

                return o;
            }

            inline float2 RemapUV(float2 rawUV, float4 rect)
            {
                return rect.xy + rawUV * rect.zw;
            }

            inline fixed SampleShadowAlpha(float2 rawUV, float4 rect)
            {
                // Scale same as sprite so shadow silhouette matches leaf shape
                float2 scaled = (rawUV - 0.5) / _SpriteScale + 0.5;
                float2 uv = RemapUV(scaled, rect);
                // Mask out samples past the quad edge to prevent wrap-around
                float2 inside = step(0.0, rawUV) * step(rawUV, 1.0);
                return tex2D(_MainTex, uv).a * inside.x * inside.y;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                // Bounds check: outside [0,1] after scaling is beyond the sprite
                float2 spriteUV = (i.rawUV - 0.5) / _SpriteScale + 0.5;
                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float spriteMask = inBounds.x * inBounds.y;

                float2 shadowRaw = i.rawUV + i.localShadowOffset;
                float s = _ShadowSoftness;

                fixed shadowAlpha;
                if (s > 0.001)
                {
                    shadowAlpha = (
                        SampleShadowAlpha(shadowRaw + float2(-s, -s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( 0, -s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( s, -s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2(-s,  0), i.uvRect) +
                        SampleShadowAlpha(shadowRaw,                  i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( s,  0), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2(-s,  s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( 0,  s), i.uvRect) +
                        SampleShadowAlpha(shadowRaw + float2( s,  s), i.uvRect)
                    ) / 9.0;
                }
                else
                {
                    shadowAlpha = SampleShadowAlpha(shadowRaw, i.uvRect);
                }

                // No light, no shadow — opacity follows intensity (clamped at authored).
                float shadowFade = _SceneLightColor.a > 0.5 ? saturate(_SceneLightIntensity) : 1.0;
                fixed4 shadow = fixed4(_ShadowColor.rgb, _ShadowColor.a * shadowAlpha * shadowFade);

                fixed4 col = tex2D(_MainTex, i.uv);
                float4 tint = UNITY_ACCESS_INSTANCED_PROP(Props, _LeafTint);
                col *= tint * _LeafColor;
                col.a *= spriteMask;

                // Diffuse term: the leaf body is lit by the scene light — same response as
                // Sprite/Diffuse. The highlight below carries its own tint.
                col.rgb *= lerp(float3(1.0, 1.0, 1.0), SceneLightTint(), _LightInfluence);

                // Specular highlight: radial falloff from offset center, masked to leaf shape
                float2 hlCenter = spriteUV - 0.5 + i.localHighlightOffset / _SpriteScale;
                float hlDist = length(hlCenter);
                float hlFalloff = 1.0 - smoothstep(_HighlightSize, _HighlightSize + _HighlightSoftness, hlDist);
                float hlMask = hlFalloff * col.a;
                // Highlight colour × the scene light's tint (a dim/tinted light dims/tints the
                // glint); softness/alpha math above is untouched.
                col.rgb = lerp(col.rgb, _HighlightColor.rgb * SceneLightTint(), hlMask * _HighlightColor.a);

                // Composite: shadow behind, leaf on top (Porter-Duff "over")
                fixed3 rgb = col.rgb * col.a + shadow.rgb * shadow.a * (1.0 - col.a);
                fixed  a   = col.a + shadow.a * (1.0 - col.a);
                return fixed4(rgb / max(a, 0.001), a);
            }
            ENDCG
        }
    }
}

