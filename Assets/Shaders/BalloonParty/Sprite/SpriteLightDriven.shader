Shader "BalloonParty/Sprite/LightDriven"
{
    // A sprite accessory fully driven by the global scene light (_SceneLightDir / Color /
    // Intensity): placement (orbit down-light of the rest position), orientation (rotate the
    // authored baked direction down-light), and colour response — each independently
    // toggleable so both archetypes fit:
    //   baked GROUND SHADOW — orbit ON, rotate OFF, fade-with-intensity ON (no light, no shadow);
    //   baked GLINT/SHINE   — orbit + rotate + tint + fade ON (the sprite IS reflected light,
    //     so it takes the light's colour AND vanishes without it).
    //
    // Baked Direction: the direction the art points as authored, in degrees (0 = +X/right,
    //                  CCW, world style). A shadow drawn toward lower-right = -45.
    // The rotation hinges on the sprite's PIVOT: author the pivot at the orbit centre
    // (e.g. the balloon's centre for its shadow) so the sprite swings around the object,
    // not around its own middle.
    // The transform's own world rotation is compensated (from its world X axis — uniform
    // scale assumed), so a swaying parent doesn't drag the baked direction with it.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Light Rotation)]
        // Rotating the art suits direction-baked shapes (a stretched glint, a directional
        // streak). A baked 2D ground shadow should usually NOT rotate — its squashed shape
        // encodes the ground perspective, not the light — turn this off and use the orbit only.
        [ToggleUI] _RotateArt ("Rotate Art To Light", Float) = 1
        _BakedAngle ("Baked Direction (deg)", Range(-180, 180)) = -45
        // Places the sprite down-light of its rest position (world units at scale 1, scaled by
        // the transform's world scale) — replaces an authored transform offset like a baked
        // shadow child's (0.04, -0.04): zero the child's localPosition and put the magnitude
        // here, so the placement orbits with the light. 0 = rotation only.
        _OrbitDistance ("Down-Light Offset", Range(0, 0.5)) = 0

        [Header(Light Response)]
        // Glint archetype: the sprite is reflected light, so it takes the scene light's
        // colour x intensity (multiplied into the sprite, like the derived speculars).
        [ToggleUI] _TintBySceneLight ("Tint By Scene Light (glints)", Float) = 0
        // Shadow archetype: no light means no shadow — opacity follows the light's intensity
        // (clamped at the authored alpha; a brighter-than-neutral light can't over-darken).
        [ToggleUI] _FadeWithSceneLight ("Fade With Light Intensity (shadows)", Float) = 0
        // Where the tint colour (above) comes from: Full = the field (ambient + local lights),
        // Ambient = the global scene light only, Local = only nearby field lights ABOVE the ambient
        // (neutral until a point/area light is near, then it brightens/tints toward it — the glint
        // ignores the global light entirely). Only matters when Tint By Scene Light is on.
        [Enum(Full, 0, Ambient, 1, Local, 2)] _LightMode ("Tint Light Mode", Float) = 0

        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
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
            #pragma target 3.5
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 texcoord   : TEXCOORD0;
                // Sampled ONCE per sprite (its own anchor, vertex stage, BEFORE the
                // orbit/rotation offset below) so the fragment's tint/fade reads the same
                // light as the placement — the PaintBlob pattern.
                float3 lightTint  : TEXCOORD1;
                float  shadowFade : TEXCOORD2;
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

            sampler2D _MainTex;
            fixed4 _Color;
            float _RotateArt;
            float _BakedAngle;
            float _OrbitDistance;
            float _TintBySceneLight;
            float _FadeWithSceneLight;
            float _LightMode;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                // Swing the sprite so the authored baked direction lies down-light in WORLD
                // space: delta = (down-light angle) - (baked angle) - (transform's own world
                // rotation, from its world X axis — uniform scale assumed, see PaintBlob).
                float2 worldX = float2(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10);
                float objAngle = (dot(worldX, worldX) > 1e-8) ? atan2(worldX.y, worldX.x) : 0.0;

                // Sampled at the object's own anchor (its pivot, BEFORE the orbit offset
                // below moves the vertex) — VTF (target 3.5), the PaintBlob precedent.
                float2 anchorWorld = float2(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13);
                float2 downLight = -SceneLightDirectionAtLOD(anchorWorld);
                OUT.shadowFade = ShadowLightFadeAtLOD(anchorWorld);

                // Tint source (see _LightMode). Local is neutral (1) until a local light is near, so the
                // glint keeps its authored look at rest and never reacts to the global ambient.
                if (_LightMode > 1.5)
                {
                    OUT.lightTint = float3(1.0, 1.0, 1.0) + SceneLightLocalAtLOD(anchorWorld);
                }
                else if (_LightMode > 0.5)
                {
                    OUT.lightTint = SceneLightTint();
                }
                else
                {
                    OUT.lightTint = SceneLightTintAtLOD(anchorWorld);
                }

                // Rotation is optional: a baked ground shadow keeps its authored shape (orbit
                // only) and behaves like a plain sprite here.
                float delta = _RotateArt > 0.5
                    ? atan2(downLight.y, downLight.x) - radians(_BakedAngle) - objAngle
                    : 0.0;

                float s = sin(delta);
                float c = cos(delta);
                float4 v = IN.vertex;
                v.xy = float2(v.x * c - v.y * s, v.x * s + v.y * c);

                // Orbit: place the sprite down-light of its rest position in WORLD space, so a
                // transform-authored offset (e.g. the baked shadow's (0.04, -0.04)) becomes a
                // light-tracked placement. Scaled by the transform's world scale (|worldX|) so
                // bigger balloons keep proportionally bigger offsets, matching the old
                // localPosition behavior.
                float4 worldPos = mul(unity_ObjectToWorld, v);
                worldPos.xy += downLight * (_OrbitDistance * length(worldX));

                OUT.vertex = UnityWorldToClipPos(worldPos);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif

                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                fixed4 c = tex2D(_MainTex, IN.texcoord) * IN.color;

                // Glint archetype: the sprite is reflected light — take the light's colour × intensity.
                if (_TintBySceneLight > 0.5)
                {
                    c.rgb *= IN.lightTint;
                }

                // Shadow archetype: no light, no shadow — opacity follows intensity, clamped at the
                // authored alpha so a hotter-than-neutral light can't over-darken. IN.shadowFade is
                // already 1.0 when the owner hasn't pushed yet, so no separate validity guard needed.
                if (_FadeWithSceneLight > 0.5)
                {
                    c.a *= IN.shadowFade;
                }

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
