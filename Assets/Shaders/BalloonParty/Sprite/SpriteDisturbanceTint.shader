// A sprite tinted by the shared disturbance field at its world position. The field's A channel packs a
// palette tag (index + remaining life); the sprite's colour is multiplied toward that palette colour by the
// tag's life (its strength). The G/B displacement direction can also swing the sprite to face the flow
// (eased by the field magnitude, offset by a base angle) and/or brighten it along that direction.
// Reads the field via globals the DisturbanceFieldService publishes (_DisturbanceTex, _FieldBoundsMin/Size,
// _DisturbancePalette). Vertex texture fetch is used for the per-sprite direction — needs Vulkan/Metal (SM3.5+).
Shader "BalloonParty/Sprite/DisturbanceTint"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor("Renderer Color", Color) = (1,1,1,1)

        [Header(Disturbance Tint)]
        _TintIntensity("Tint Intensity", Range(0,4)) = 1

        [Header(Direction)]
        [Toggle(_ROTATE_ON)] _RotateToDirection("Rotate To Direction", Float) = 0
        _BaseAngle("Base Angle (deg)", Float) = 0
        _DirectionStrength("Direction Strength", Range(0,8)) = 4
        [Toggle(_GRADIENT_ON)] _DirectionGradient("Direction Gradient", Float) = 0
        _GradientStrength("Gradient Strength", Range(0,2)) = 0.5

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

            // Published as globals by DisturbanceFieldService.
            sampler2D _DisturbanceTex;
            float4 _FieldBoundsMin;
            float4 _FieldBoundsSize;
            float4 _DisturbancePalette[16];
            int _DisturbancePaletteCount;

            float _TintIntensity;
            float _BaseAngle;
            float _DirectionStrength;
            float _GradientStrength;

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

                // Sprite pivot in world = object origin; the direction is sampled there (per-sprite).
                float2 centerWorld = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                float4 v = IN.vertex;
                float2 dir = float2(0, 0);
                float mag = 0.0;

                #if defined(_ROTATE_ON) || defined(_GRADIENT_ON)
                    float4 fc = tex2Dlod(_DisturbanceTex, float4(FieldUV(centerWorld), 0, 0));
                    dir = (fc.gb - 0.5) * 2.0;
                    mag = saturate(length(dir) * _DirectionStrength);
                #endif

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

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;

                float2 uv = FieldUV(IN.worldPos);
                if (all(uv >= 0.0) && all(uv <= 1.0))
                {
                    // A packs (index + life)/16: ceil recovers the slot, the remainder is the tag's life = strength.
                    float tagValue = tex2D(_DisturbanceTex, uv).a * 16.0;
                    int index = (int)(ceil(tagValue) - 1.0);
                    if (tagValue > 0.05 && index >= 0 && index < _DisturbancePaletteCount)
                    {
                        float strength = saturate((tagValue - (float)index) * _TintIntensity);
                        c.rgb *= lerp(float3(1, 1, 1), _DisturbancePalette[index].rgb, strength);
                    }

                    #ifdef _GRADIENT_ON
                    float2 offset = IN.worldPos - IN.centerWorld;
                    float g = dot(normalize(offset + 1e-5), IN.dir);
                    c.rgb *= 1.0 + _GradientStrength * g * IN.mag;
                    #endif
                }

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
