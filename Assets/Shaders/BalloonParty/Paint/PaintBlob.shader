Shader "BalloonParty/Paint/PaintBlob"
{
    Properties
    {
        _Color              ("Paint Color",          Color)              = (1, 0.2, 0.2, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Blob Shape)]
        _SpriteScale        ("Sprite Scale",         Range(0.50, 1.00))  = 1.0
        _BlobRadius         ("Base Radius",          Range(0.10, 0.50))  = 0.40
        _EdgeSoftness       ("Edge Softness",        Range(0.001, 0.05)) = 0.012

        [Header(Wobble)]
        _TimeOffset         ("Time Offset",          Float)              = 0.0
        _WobbleSpeed        ("Speed",                Range(0, 6))        = 1.4
        _WobbleAmplitude    ("Amplitude",            Range(0, 0.08))     = 0.028
        _WobbleFrequency    ("Frequency  (lobes)",   Range(1, 8))        = 4.0
        _WobbleSpeed2       ("Speed 2",              Range(0, 6))        = 2.3
        _WobbleAmplitude2   ("Amplitude 2",          Range(0, 0.04))     = 0.012
        _WobbleFrequency2   ("Frequency 2  (lobes)", Range(1, 12))       = 7.0

        [Header(Surface)]
        _RimWidth           ("Rim Width",            Range(0.05, 2.0))   = 0.55
        _RimDarkness        ("Rim Darkness",         Range(0, 1))        = 0.45
        _SpecularColor      ("Specular Color",       Color)              = (1, 1, 1, 0.85)
        _SpecularSize       ("Specular Size",        Range(0.01, 0.40))  = 0.14
        _SpecularSharpness  ("Specular Sharpness",   Range(1, 20))       = 7.0
        _SpecularDistance   ("Specular Distance",    Range(0, 0.4))      = 0.23

        [Header(Light Response)]
        // Diffuse response to the scene light (albedo x colour x intensity, like Sprite/Diffuse):
        // 0 = unlit (authored look always), 1 = fully lit.
        _LightInfluence ("Light Influence", Range(0, 1)) = 1

        [Header(Shadow)]
        [Toggle(_SHADOW_ON)] _EnableShadow ("Enable Shadow", Float) = 0
        _ShadowColor        ("Shadow Color",   Color)              = (0.15, 0.15, 0.15, 0.6)
        _ShadowDistance     ("Shadow Distance", Range(0, 0.3))      = 0.036
        _ShadowSoftness     ("Shadow Softness", Range(0.001, 0.08)) = 0.02
        _ShadowScale        ("Shadow Scale",    Range(0.10, 3.00))  = 1.0
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
                float2 spin     : TEXCOORD1;
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

            float  _SpriteScale;
            float  _BlobRadius;
            float  _EdgeSoftness;

            float  _WobbleSpeed;
            float  _WobbleAmplitude;
            float  _WobbleFrequency;
            float  _WobbleSpeed2;
            float  _WobbleAmplitude2;
            float  _WobbleFrequency2;
            float  _TimeOffset;

            float  _RimWidth;
            float  _RimDarkness;
            fixed4 _SpecularColor;
            float  _SpecularSize;
            float  _SpecularSharpness;
            float  _SpecularDistance;
            float  _LightInfluence;

            // Set globally by SceneLightService; kept out of Properties so no
            // material value can shadow the scene-wide light. Colour's alpha is the
            // "owner has pushed" validity flag (see SceneLightTint).
            float4 _SceneLightDir;
            float4 _SceneLightColor;
            float  _SceneLightIntensity;

            #ifdef _SHADOW_ON
            fixed4 _ShadowColor;
            float  _ShadowDistance;
            float  _ShadowSoftness;
            float  _ShadowScale;
            #endif

            // Normalized scene light direction, with an edit-time fallback for
            // the degenerate case (service not yet run / zero vector).
            float2 SceneLight()
            {
                float2 dir = _SceneLightDir.xy;
                return (dot(dir, dir) > 1e-8) ? normalize(dir) : float2(-0.707, 0.707);
            }

            // The light's colour × intensity — multiplies into the authored specular response.
            // Neutral (white) when the owner hasn't pushed yet, so nothing dims at edit time.
            float3 SceneLightTint()
            {
                return _SceneLightColor.a > 0.5
                    ? _SceneLightColor.rgb * _SceneLightIntensity
                    : float3(1.0, 1.0, 1.0);
            }

            // No light, no shadow: the shadow's opacity follows the light's intensity (clamped at the
            // authored alpha). Neutral when the owner hasn't pushed yet (edit time).
            float ShadowLightFade()
            {
                return _SceneLightColor.a > 0.5 ? saturate(_SceneLightIntensity) : 1.0;
            }

            // Computes the blob SDF boundary at a given UV offset from center.
            // Returns the alpha (0 = outside, 1 = inside) using the wobble + edge softness.
            float BlobAlpha(float2 uv, float edgeSoftness)
            {
                float  r   = length(uv);
                float  t   = _Time.y + _TimeOffset;
                float2 dir = (r > 0.0001) ? (uv / r) : float2(1.0, 0.0);

                float2 z1 = float2(1.0, 0.0);
                int    n1 = (int)round(_WobbleFrequency);
                for (int i = 0; i < n1; i++)
                    z1 = float2(z1.x*dir.x - z1.y*dir.y, z1.x*dir.y + z1.y*dir.x);
                float w1 = z1.y * cos(_WobbleSpeed * t) + z1.x * sin(_WobbleSpeed * t);

                float2 z2 = float2(1.0, 0.0);
                int    n2 = (int)round(_WobbleFrequency2);
                for (int j = 0; j < n2; j++)
                    z2 = float2(z2.x*dir.x - z2.y*dir.y, z2.x*dir.y + z2.y*dir.x);
                float w2 = z2.y * cos(_WobbleSpeed2 * t) - z2.x * sin(_WobbleSpeed2 * t);

                float wobble   = _WobbleAmplitude * w1 + _WobbleAmplitude2 * w2;
                float boundary = _BlobRadius + wobble;
                float sdf      = boundary - r;
                return smoothstep(0.0, edgeSoftness, sdf);
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color * _RendererColor;

                // (cos, sin) of the object's world rotation, so the fragment
                // can undo it and keep the specular fixed in world space.
                float2 worldX = float2(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10);
                OUT.spin = (dot(worldX, worldX) > 1e-8) ? normalize(worldX) : float2(1.0, 0.0);
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                float2 uv  = (IN.texcoord - 0.5) / _SpriteScale;
                float  r   = length(uv);
                float  t   = _Time.y + _TimeOffset;

                // ── Main blob alpha ──
                float alpha = BlobAlpha(uv, _EdgeSoftness);

                // ── Shadow (composited behind the blob) ──
                #ifdef _SHADOW_ON
                // The shadow lands down-light: world direction derived from the scene light,
                // only the distance stays authored. Inverse-rotated (R(-θ), spin = (cosθ, sinθ))
                // into the blob's local frame so the silhouette wobbles in the blob's own frame
                // AND stays world-anchored while the sprite spins (the authored offset used to
                // rotate with it).
                float2 shadowWorld = -SceneLight() * _ShadowDistance;
                float2 shadowLocal = float2( shadowWorld.x * IN.spin.x + shadowWorld.y * IN.spin.y,
                                            -shadowWorld.x * IN.spin.y + shadowWorld.y * IN.spin.x);
                float2 shadowUV    = (uv - shadowLocal) / max(_ShadowScale, 0.001);
                float  shadowAlpha = BlobAlpha(shadowUV, _ShadowSoftness / max(_ShadowScale, 0.001)) * _ShadowColor.a;
                shadowAlpha *= ShadowLightFade();
                #endif

                // Early discard — nothing to draw if both blob and shadow are invisible
                #ifdef _SHADOW_ON
                if (alpha < 0.001 && shadowAlpha < 0.001) discard;
                #else
                if (alpha < 0.001) discard;
                #endif

                // ── Blob surface shading ──
                float2 dir = (r > 0.0001) ? (uv / r) : float2(1.0, 0.0);

                float2 z1 = float2(1.0, 0.0);
                int    n1 = (int)round(_WobbleFrequency);
                for (int i = 0; i < n1; i++)
                    z1 = float2(z1.x*dir.x - z1.y*dir.y, z1.x*dir.y + z1.y*dir.x);
                float w1 = z1.y * cos(_WobbleSpeed  * t) + z1.x * sin(_WobbleSpeed  * t);

                float2 z2 = float2(1.0, 0.0);
                int    n2 = (int)round(_WobbleFrequency2);
                for (int j = 0; j < n2; j++)
                    z2 = float2(z2.x*dir.x - z2.y*dir.y, z2.x*dir.y + z2.y*dir.x);
                float w2 = z2.y * cos(_WobbleSpeed2 * t) - z2.x * sin(_WobbleSpeed2 * t);

                float wobble   = _WobbleAmplitude * w1 + _WobbleAmplitude2 * w2;
                float boundary = _BlobRadius + wobble;

                // Radial gradient for rim darkening
                float innerT  = saturate(r / max(boundary, 0.0001));
                float rimMask = pow(innerT, 1.0 / max(_RimWidth, 0.001));
                fixed3 col    = IN.color.rgb * (1.0 - rimMask * _RimDarkness);

                // Diffuse term: the blob body is albedo, lit by the scene light — same response
                // as Sprite/Diffuse. Without this the blob reads self-illuminated when the light
                // dims. The specular below carries its own light scaling (no double-apply).
                col *= lerp(float3(1.0, 1.0, 1.0), SceneLightTint(), _LightInfluence);

                // Specular highlight — sampled in world orientation so it stays
                // put regardless of how the parent transform is rotated.
                float2 worldUV = float2(uv.x * IN.spin.x - uv.y * IN.spin.y,
                                        uv.x * IN.spin.y + uv.y * IN.spin.x);
                float2 specCenter = SceneLight() * _SpecularDistance;
                float  specDist   = length(worldUV - specCenter);
                float  specMask   = pow(saturate(1.0 - specDist / max(_SpecularSize, 0.001)),
                                        _SpecularSharpness);
                // Specular response = authored colour × the scene light's tint (a dim/tinted
                // light dims/tints the glint).
                col = lerp(col, _SpecularColor.rgb * SceneLightTint(), specMask * _SpecularColor.a);

                // ── Composite: shadow under blob (Porter-Duff "over") ──
                #ifdef _SHADOW_ON
                fixed3 shadowRGB = _ShadowColor.rgb;
                fixed  combinedA = alpha + shadowAlpha * (1.0 - alpha);
                fixed3 combinedRGB = combinedA > 0.0001
                    ? (col * alpha + shadowRGB * shadowAlpha * (1.0 - alpha)) / combinedA
                    : col;
                return fixed4(combinedRGB, combinedA);
                #else
                return fixed4(col, alpha);
                #endif
            }
            ENDCG
        }
    }
}

