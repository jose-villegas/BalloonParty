Shader "BalloonParty/Scenario/BackgroundCloud"
{
    // The full-screen cloud backdrop. It no longer generates its own noise — it renders FROM the shared
    // cloud-density RT (BackgroundFieldService bakes it; see BackgroundField.cginc), so the cloud SHAPE has a single
    // source (the blit material) and this material only owns the LOOK: colour, scene lighting, drop shadow.
    // The shading normal is derived from the density's own gradient (screen-space derivatives), and the
    // soft drop shadow is an offset tap of the same map. Assign to a full-screen SpriteRenderer (the
    // Scenario Background).
    Properties
    {
        _Color              ("Tint",               Color)              = (0.85, 0.95, 1.0, 1.0)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Visual)]
        _CloudColor         ("Cloud Color",        Color)              = (1, 1, 1, 0.6)

        [Header(Lighting)]
        _LightColor         ("Highlight Color",    Color)              = (1, 1, 0.95, 1)
        _AmbientColor       ("Shadow Tint",        Color)              = (0.55, 0.58, 0.7, 1)
        _LightIntensity     ("Light Intensity",    Range(0, 1))        = 0.45
        // Diffuse response to the scene light (colour x intensity multiplies the whole cloud):
        // 0 = unlit (authored look always), 1 = fully lit.
        _LightInfluence     ("Light Influence",    Range(0, 1))        = 1
        // Blends the scene-light colour into the cloud's shape: 0 = uniform colour overlay,
        // 1 = colour tracks the cloud density (strong in the core, absent at edges).
        _ColorNoiseBias     ("Color Noise Bias",   Range(0, 1))        = 0.5
        _NormalStrength     ("Normal Strength",    Range(0, 3))        = 1.2
        _NormalEpsilon      ("Normal Sample Offset",Range(0.001, 0.05))= 0.012

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow",   Float)  = 1
        _ShadowColor        ("Shadow Color",       Color)              = (0.06, 0.06, 0.14, 0.35)
        _ShadowDistance     ("Shadow Distance",    Range(0, 3.0))      = 0.6
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
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma shader_feature _SHADOW_ON
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/BackgroundField.cginc"

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
                float2 worldPos : TEXCOORD0;
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
            fixed4 _CloudColor;
            fixed4 _LightColor;
            fixed4 _AmbientColor;
            float  _LightIntensity;
            float  _LightInfluence;
            float  _ColorNoiseBias;
            float  _NormalStrength;
            float  _NormalEpsilon;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowDistance;
            float  _ShadowSoftness;
            #endif

            // Half-Lambert scene lighting from a pseudo-normal built out of the density gradient (the
            // GPU's screen-space derivatives), then the scene diffuse (colour x intensity) eased by
            // _LightInfluence. Same shape as PuffCloud's CloudLighting; the gradient now comes from the
            // baked density instead of a raw noise octave.
            fixed3 CloudLighting(float2 worldGradient, float2 worldPos, float cloudDensity)
            {
                float dX = clamp(worldGradient.x * 2.0 * _NormalEpsilon * _NormalStrength, -1.5, 1.5);
                float dY = clamp(worldGradient.y * 2.0 * _NormalEpsilon * _NormalStrength, -1.5, 1.5);

                float3 normal = normalize(float3(-dX, -dY, 1.0));

                float2 ld = SceneLightDirectionAt(worldPos);
                float3 lightVec = normalize(float3(ld, 0.6));

                float NdotL = dot(normal, lightVec);
                float halfLambert = NdotL * 0.5 + 0.5;

                fixed3 lit = lerp(_AmbientColor.rgb, _LightColor.rgb, halfLambert);
                fixed3 shading = lerp(fixed3(1, 1, 1), lit, _LightIntensity);

                float localMag = _SceneLightFieldOn > 0.5
                    ? saturate(SceneLightFieldSample(worldPos).r)
                    : 0.0;
                float biasStrength = _ColorNoiseBias * localMag;
                float influence = lerp(_LightInfluence, _LightInfluence * cloudDensity, biasStrength);
                float3 sceneTint = SceneLightTintAt(worldPos);
                return shading * lerp(float3(1.0, 1.0, 1.0), sceneTint, influence);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;

                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.color    = IN.color * _Color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;

                // Cloud shape = the shared, baked density map. Computed before any discard so the
                // screen-space derivatives below stay defined.
                float cloud = BackgroundFieldDensity(wp);

                // Shading normal from the density's own gradient. Pixel world size assumes the camera
                // never rotates (it doesn't).
                float2 pixelWorld = max(float2(abs(ddx(wp.x)), abs(ddy(wp.y))), 1e-5);
                float2 lightGradient = float2(ddx(cloud), ddy(cloud)) / pixelWorld;

                float mainAlpha = cloud * _CloudColor.a * IN.color.a;
                fixed3 mainRgb = _CloudColor.rgb * IN.color.rgb * CloudLighting(lightGradient, wp, cloud);

                #ifdef _SHADOW_ON
                // Soft drop shadow composited BEHIND the cloud — an offset tap of the same density map,
                // down-light of the fragment (rotates with the scene light).
                float2 shadowWp = wp + SceneLightDirectionAt(wp) * _ShadowDistance;
                float shadowAlpha = 0.0;
                if (mainAlpha < 0.999)
                {
                    float shadowCloud = BackgroundFieldDensity(shadowWp);
                    shadowAlpha = shadowCloud * _ShadowColor.a * IN.color.a;
                    shadowAlpha *= smoothstep(0.0, _ShadowSoftness + 0.01, shadowCloud);
                    shadowAlpha *= ShadowLightFadeAt(wp);
                }

                if (cloud < 0.001 && shadowAlpha < 0.001)
                {
                    discard;
                }

                if (cloud < 0.001)
                {
                    return fixed4(_ShadowColor.rgb * shadowAlpha, shadowAlpha);
                }

                // Pre-multiplied composite: rgb already weighted by alpha
                fixed  combinedA  = mainAlpha + shadowAlpha * (1.0 - mainAlpha);
                fixed3 combinedPM = mainRgb * mainAlpha + _ShadowColor.rgb * shadowAlpha * (1.0 - mainAlpha);

                return fixed4(combinedPM, combinedA);
                #else
                if (cloud < 0.001)
                {
                    discard;
                }

                return fixed4(mainRgb * mainAlpha, mainAlpha);
                #endif
            }
            ENDCG
        }
    }
}
