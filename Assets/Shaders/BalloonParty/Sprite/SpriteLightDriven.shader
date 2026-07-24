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
        // Which lights steer the down-light direction (used for BOTH the swing and the orbit below):
        // Full = the field (ambient + local lights), Ambient = the global scene light only, Local = only
        // nearby field lights — the art holds its baked orientation/placement at rest and swings/orbits
        // toward a local light as it approaches (a glint that turns to face a passing spark).
        [Enum(Full, 0, Ambient, 1, Local, 2)] _RotateLightMode ("Rotation Light Mode", Float) = 0
        _BakedAngle ("Baked Direction (deg)", Range(-180, 180)) = -45
        // Places the sprite down-light of its rest position (world units at scale 1, scaled by
        // the transform's world scale) — replaces an authored transform offset like a baked
        // shadow child's (0.04, -0.04): zero the child's localPosition and put the magnitude
        // here, so the placement orbits with the light. 0 = rotation only.
        _OrbitDistance ("Down-Light Offset", Range(0, 0.5)) = 0
        // 0 = a single point sample at the sprite's anchor (the old behaviour). >0 integrates the
        // field over a disc of this world-space radius (anchor + 4 rim taps) so a light sweeping
        // across the sprite's footprint turns the response smoothly instead of flipping the instant
        // its peak crosses one texel — tune it toward the sprite's visual size for accessories that
        // sit near light sources (a glint on a balloon a laser sweeps past).
        _ReceiverRadius ("Receiver Radius (disc integration)", Range(0, 1)) = 0

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
        // The opacity floor at minimal light — used by both Local mode (opacity at rest, no nearby
        // light) and Alpha Follows Light below (opacity where the scene is darkest). 0 = invisible
        // until light arrives (a spark that flares in); 1 = always-visible. No effect in Full/Ambient
        // modes unless Alpha Follows Light is on.
        _RestAlpha ("Resting Alpha (floor)", Range(0, 1)) = 0
        // OPT-IN (specular/glint archetype): ramp opacity with the MAGNITUDE of the light colour — the
        // tint selected by Tint Light Mode. Low light -> Resting Alpha, bright light -> full. Unlike
        // Fade With Light Intensity (which keys off the scalar intensity), this reads the colour
        // vector's length, so it darkens as the day/night gradient darkens and, in Full mode, ramps up
        // near local lights. For a baked specular highlight, pair with Tint Light Mode = Full.
        [ToggleUI] _AlphaFollowsLight ("Alpha Follows Light Magnitude", Float) = 0
        // The light-colour magnitude that maps to FULL alpha (~1.73 = white daylight at intensity 1).
        // Normalizing against this means daylight always saturates to full regardless of the falloff
        // below — lower it so the highlight reaches full in dimmer light, raise it to need brighter.
        _AlphaFullAt ("Alpha Full-Light Level", Range(0.1, 3)) = 1.73
        // Shapes the ramp BELOW full without touching the ceiling: 1 = linear, >1 = the highlight stays
        // concentrated in bright light and drops off fast toward dusk, <1 = it lingers into dim light.
        _AlphaFalloff ("Alpha Falloff", Range(0.25, 4)) = 1
        // Extra alpha ramp from LOCAL lights only, ON TOP of the base colour magnitude — raise so a
        // passing point/area light flares the highlight harder than daylight alone (which saturates
        // the base ramp and so can't push it further). 0 = local counts only via the combined tint.
        _LocalAlphaBoost ("Local Light Alpha Boost", Range(0, 8)) = 0

        [Header(Cloud Field)]
        // OPT-IN (shadow archetype): fade this sprite's alpha by the shared cloud field so a baked ground
        // shadow sinks into the BackgroundCloud layer, surviving where the cloud is and dissolving on
        // no-cloud texels. Leave off for glints and non-shadow accessories.
        [Toggle(_CLOUD_FADE_ON)] _CloudShadowFade ("Fade By Cloud Field", Float) = 0
        _CloudShadowFloor ("Cloud Fade Floor", Range(0, 1)) = 0.0
        // Smoke opacity from the painting field acts as a second shadow-receiving surface;
        // 0 = old behavior (clouds only).
        _SmokeReceiveWeight ("Smoke Shadow Receive", Range(0, 1)) = 0

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
            #pragma shader_feature_local _CLOUD_FADE_ON
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/BackgroundField.cginc"
            #include "../Include/PaintingField.cginc"

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
                float3 lightTint   : TEXCOORD1;
                float  shadowFade  : TEXCOORD2;
                // 0..1 "how much local light" — drives the resting-alpha fade in Local mode; 1 otherwise.
                float  localAmount : TEXCOORD3;
                // Local-light magnitude above ambient, for Alpha Follows Light's local boost; 0 when the
                // feature is off (sampled only then, so ordinary uses pay nothing).
                float  localBoost  : TEXCOORD5;
                #ifdef _CLOUD_FADE_ON
                float2 cloudWorld  : TEXCOORD4;
                #endif
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
            float _RotateLightMode;
            float _BakedAngle;
            float _OrbitDistance;
            float _ReceiverRadius;
            float _TintBySceneLight;
            float _FadeWithSceneLight;
            float _LightMode;
            float _RestAlpha;
            float _AlphaFollowsLight;
            float _AlphaFullAt;
            float _AlphaFalloff;
            float _LocalAlphaBoost;
            float _CloudShadowFloor;
            float _SmokeReceiveWeight;

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

                // Down-light FLOW feeding BOTH the swing and the orbit, scoped by _RotateLightMode. Flow
                // (see SceneLight.cginc) is never normalized — its length IS the confidence in its
                // direction, so dirAmount comes straight from it instead of a separate out-param. Local
                // yields a rest length of 0 (art keeps its baked orientation/placement) that grows as a
                // local light nears; Ambient is always full confidence (the flat global can't flip);
                // Full carries the field's own confidence, which is what fixes the perpendicular flip —
                // it dips toward 0 (swing/orbit fade through rest) instead of snapping 180 when a local
                // light opposes the ambient or its peak crosses the anchor.
                float dirAmount;
                float2 downLight;
                if (_RotateLightMode > 1.5)
                {
                    float2 flow = SceneLightLocalFlowAtLOD(anchorWorld, _ReceiverRadius);
                    dirAmount = saturate(length(flow));
                    downLight = -flow;
                }
                else if (_RotateLightMode > 0.5)
                {
                    dirAmount = 1.0;
                    downLight = -SceneLightDirection();
                }
                else
                {
                    float2 flow = SceneLightFlowAtLOD(anchorWorld, _ReceiverRadius);
                    dirAmount = saturate(length(flow));
                    downLight = -flow;
                }
                OUT.shadowFade = ShadowLightFadeAtLOD(anchorWorld);

                // Tint source (see _LightMode). Local is neutral (1) until a local light is near, so the
                // glint keeps its authored look at rest and never reacts to the global ambient; its
                // brightest channel doubles as the 0..1 local-presence that drives the resting-alpha fade.
                if (_LightMode > 1.5)
                {
                    float3 local = SceneLightLocalAtLOD(anchorWorld);
                    OUT.lightTint = float3(1.0, 1.0, 1.0) + local;
                    OUT.localAmount = saturate(max(local.r, max(local.g, local.b)));
                }
                else if (_LightMode > 0.5)
                {
                    OUT.lightTint = SceneLightTint();
                    OUT.localAmount = 1.0;
                }
                else
                {
                    OUT.lightTint = SceneLightTintAtLOD(anchorWorld);
                    OUT.localAmount = 1.0;
                }

                // Extra alpha sensitivity to LOCAL lights (Alpha Follows Light): the local boost above
                // ambient, sampled here only when the feature is on so shadows/glints pay nothing.
                OUT.localBoost = (_AlphaFollowsLight > 0.5) ? length(SceneLightLocalAtLOD(anchorWorld)) : 0.0;

                // Rotation is optional: a baked ground shadow keeps its authored shape (orbit
                // only) and behaves like a plain sprite here.
                // dirAmount fades the swing by the flow's own confidence in ALL modes (generalized from
                // the old Local-only pattern): 0 keeps the baked angle, 1 = full swing to the light.
                // Ambient/rest-Full pass dirAmount 1 (bit-identical to before); it only dips below 1 near
                // a cancellation, which is exactly where atan2(downLight) would otherwise be unstable —
                // atan2 on a near-zero flow is technically undefined, but harmless here since dirAmount
                // ~0 kills the swing amplitude before that angle ever shows up.
                float delta = _RotateArt > 0.5
                    ? (atan2(downLight.y, downLight.x) - radians(_BakedAngle) - objAngle) * dirAmount
                    : 0.0;

                float s = sin(delta);
                float c = cos(delta);
                float4 v = IN.vertex;
                v.xy = float2(v.x * c - v.y * s, v.x * s + v.y * c);

                // Orbit: place the sprite down-light of its rest position in WORLD space, so a
                // transform-authored offset (e.g. the baked shadow's (0.04, -0.04)) becomes a
                // light-tracked placement. Scaled by the transform's world scale (|worldX|) so
                // bigger balloons keep proportionally bigger offsets, matching the old
                // localPosition behavior. downLight is UNNORMALIZED flow, so its own magnitude already
                // carries the confidence — no separate dirAmount factor here (double-counting it would
                // square the fade). At rest that magnitude is exactly 1 (ambient-only flow), so this
                // matches the old orbit distance bit-for-bit; near a cancellation it shrinks toward 0 and
                // grows back on the other side, so the shadow SLIDES through its rest point instead of
                // teleporting across it.
                float4 worldPos = mul(unity_ObjectToWorld, v);
                worldPos.xy += downLight * (_OrbitDistance * length(worldX));

                #ifdef _CLOUD_FADE_ON
                // Per-fragment world position: the shadow fades by the cloud field texel-by-texel.
                OUT.cloudWorld = worldPos.xy;
                #endif

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

                // Alpha Follows Light (specular archetype): ramp opacity by the light COLOUR's magnitude
                // (length of the tint selected by _LightMode) — low light -> _RestAlpha floor, bright ->
                // full. Reads the colour vector, not the scalar intensity, so it dims with the day/night
                // gradient and (Full mode) rises near local lights. Meant for Full/Ambient tint mode; in
                // Local mode the localAmount line below already handles the rest-fade.
                if (_AlphaFollowsLight > 0.5)
                {
                    // Normalize so full daylight (magnitude _AlphaFullAt) reaches 1 before the falloff
                    // curve, so the curve shapes the low end without ever capping the daylight ceiling.
                    float lit = saturate(length(IN.lightTint) / _AlphaFullAt);
                    float alphaDrive = pow(lit, _AlphaFalloff) + IN.localBoost * _LocalAlphaBoost;
                    c.a *= lerp(_RestAlpha, 1.0, saturate(alphaDrive));
                }

                // Local mode: rest at _RestAlpha, fade up to full as a local light nears (localAmount is 1
                // in Full/Ambient, so this is a no-op there). Before the premultiply below.
                c.a *= lerp(_RestAlpha, 1.0, IN.localAmount);

                #ifdef _CLOUD_FADE_ON
                // Shadow archetype: fade the (shadow) sprite by the shared cloud field so it sinks into
                // the backdrop, dissolving on no-cloud texels. Floor keeps a base shadow. The shadow now
                // sinks into whichever receiving surface is denser at the texel — cloud density or
                // weighted smoke opacity (max avoids double-darkening where they overlap).
                float receive = BackgroundFieldDensity(IN.cloudWorld);
                receive = max(receive, PaintingFieldSample(IN.cloudWorld).a * _SmokeReceiveWeight);
                c.a *= lerp(_CloudShadowFloor, 1.0, receive);
                #endif

                c.rgb *= c.a;
                return c;
            }
            ENDCG
        }
    }
}
