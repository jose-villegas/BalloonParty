// A sprite tinted by the shared disturbance field. The field is sampled ONCE per sprite (at its pivot, in
// the vertex stage), so the whole sprite takes a single uniform tint — no per-pixel sampling, so the field's
// low resolution never shows as blocks. The A channel's tag gives the palette colour + life (strength); the
// sprite colour is multiplied toward it. The G/B direction can rotate the sprite to face the flow (base-angle
// offset, eased by field magnitude) and/or brighten it along that direction, and the sprite fades toward a
// resting alpha where the field is quiet. Reads the field via globals the DisturbanceFieldService publishes
// (_DisturbanceTex, _FieldBoundsMin/Size, _DisturbancePalette). Vertex texture fetch — needs Vulkan/Metal (SM3.5+).
Shader "BalloonParty/Sprite/DisturbanceTint"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)

        [Header(Disturbance Tint)]
        _TintIntensity("Tint Intensity", Range(0,4)) = 1
        [PaletteColorMaskMat] _IgnoreColorMask("Ignore Colours", Float) = 0

        [Header(Direction)]
        [Toggle(_ROTATE_ON)] _RotateToDirection("Rotate To Direction", Float) = 0
        _BaseAngle("Base Angle (deg)", Float) = 0
        _DirectionStrength("Direction Strength", Range(0,8)) = 4
        [Toggle(_GRADIENT_ON)] _DirectionGradient("Direction Gradient", Float) = 0
        _GradientStrength("Gradient Strength", Range(0,2)) = 0.5

        [Header(Resting Fade)]
        _RestAlpha("Rest Alpha (field quiet)", Range(0,1)) = 1
        _ActivityScale("Activity Scale", Range(0,8)) = 2

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
            #pragma target 3.5
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ROTATE_ON
            #pragma shader_feature_local _GRADIENT_ON
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
                float4 vertex      : SV_POSITION;
                fixed4 color       : COLOR;
                float2 texcoord    : TEXCOORD0;
                float2 worldPos    : TEXCOORD1;
                float2 centerWorld : TEXCOORD2;
                float2 dir         : TEXCOORD3;
                float  mag         : TEXCOORD4;
                float3 tint        : TEXCOORD5;
                float  activity    : TEXCOORD6;
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

            sampler2D _MainTex;
            sampler2D _AlphaTex;
            float _AlphaSplitEnabled;

            // Published as globals by DisturbanceFieldService. Bilinear: with a single per-sprite sample there
            // is no spatial banding to avoid, and the smooth value tracks the field as the sprite moves.
            Texture2D _DisturbanceTex;      // R = density (activity), G/B = direction
            Texture2D _DisturbanceColorTex; // RGB = smoothed palette colour, A = strength (eased on overwrite)
            SamplerState sampler_linear_clamp;
            float4 _FieldBoundsMin;
            float4 _FieldBoundsSize;

            float _TintIntensity;
            float _IgnoreColorMask;
            float _BaseAngle;
            float _DirectionStrength;
            float _GradientStrength;
            float _RestAlpha;
            float _ActivityScale;

            float2 FieldUV(float2 worldXY)
            {
                return (worldXY - _FieldBoundsMin.xy) / max(_FieldBoundsSize.xy, 1e-4);
            }

            fixed4 SampleSpriteTexture(float2 uv)
            {
                fixed4 color = tex2D(_MainTex, uv);

                #if UNITY_TEXTURE_ALPHASPLIT_ALLOWED
                if (_AlphaSplitEnabled)
                    color.a = tex2D(_AlphaTex, uv).r;
                #endif

                return color;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // One field sample at the sprite pivot drives the whole sprite (tint, activity, direction).
                float2 centerWorld = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                float2 cuv = FieldUV(centerWorld);

                float3 tint = float3(1, 1, 1);
                float activity = 0.0;
                float2 dir = float2(0, 0);
                float mag = 0.0;

                if (all(cuv >= 0.0) && all(cuv <= 1.0))
                {
                    // Tint from the smoothed colour layer: real colours, eased on overwrite — no index decode,
                    // no banding, no flicker. RGB = colour, A = strength (the tag's life, temporally eased).
                    float4 col = _DisturbanceColorTex.SampleLevel(sampler_linear_clamp, cuv, 0);
                    tint = lerp(float3(1, 1, 1), col.rgb, saturate(col.a * _TintIntensity));

                    // Density (activity) and direction still come from the field itself.
                    float4 fc = _DisturbanceTex.SampleLevel(sampler_linear_clamp, cuv, 0);
                    float density = abs(fc.r - 0.5) * 2.0;
                    activity = saturate(max(density * _ActivityScale, col.a));

                    dir = (fc.gb - 0.5) * 2.0;
                    mag = saturate(length(dir) * _DirectionStrength);
                }

                float4 v = IN.vertex;
                #ifdef _ROTATE_ON
                    // Swing the quad toward the flow, eased by field strength, offset by the art's base angle.
                    float theta = radians(_BaseAngle) + atan2(dir.y, dir.x) * mag;
                    float sn = sin(theta);
                    float cs = cos(theta);
                    v.xy = float2(v.x * cs - v.y * sn, v.x * sn + v.y * cs);
                #endif

                OUT.vertex = UnityObjectToClipPos(v);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, v).xy;
                OUT.centerWorld = centerWorld;
                OUT.dir = mag > 1e-4 ? dir / length(dir) : float2(0, 0);
                OUT.mag = mag;
                OUT.tint = tint;
                OUT.activity = activity;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                c.rgb *= IN.tint;

                #ifdef _GRADIENT_ON
                float2 offset = IN.worldPos - IN.centerWorld;
                float g = dot(normalize(offset + 1e-5), IN.dir);
                c.rgb *= 1.0 + _GradientStrength * g * IN.mag;
                #endif

                // Fade toward the resting alpha where the field is quiet; full alpha where it's disturbed.
                c.a *= lerp(_RestAlpha, 1.0, IN.activity);
                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
