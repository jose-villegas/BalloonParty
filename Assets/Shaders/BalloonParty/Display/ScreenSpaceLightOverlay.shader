Shader "BalloonParty/Display/ScreenSpaceLightOverlay"
{
    // Fullscreen composite for ScreenSpaceLightService (see the arch_screen_space_light doc).
    // Rendered as a camera-fitted quad ABOVE all gameplay — NOT a post effect: the
    // multiplicative blend (2*src*dst, 0.5 neutral) tints the frame in place with no
    // framebuffer readback, so tile GPUs never stall. Samples the low-res light buffer
    // built by ScreenSpaceLightSmear (a = shadow amount, rgb = bounce color) and the
    // light field (SceneLight.cginc) for the per-fragment magnitude that scales both.
    Properties
    {
        [NoScaleOffset] _LightTex ("Light Buffer", 2D) = "gray" {}
        _ShadowTint     ("Shadow Tint",     Color)       = (0.55, 0.6, 0.75, 1)
        _ShadowStrength ("Shadow Strength", Range(0, 1)) = 0.35
        _BounceStrength ("Bounce Strength", Range(0, 2)) = 0.25
        // The ambient sky color the bounce is measured against — pushed by the service
        // from the camera background, so flat sky nets to neutral (no global tint).
        _AmbientColor   ("Ambient Color",   Color)       = (0.6, 0.8, 0.95, 1)
        // The most the overlay may brighten the brightest balloon (per channel). The blend can't see the frame,
        // so the cap is on the multiplier, divided by how bright the light lets a balloon get at that spot: at
        // noon a white balloon stays under the bloom start (1.02) and only emitters glow, while the dark night
        // leaves the bounce room to colour its neighbours (PLAN-HDR). Per channel: shadows and darkening are
        // untouched, but at noon a bright channel's tint goes with its brightening (scaling all three down
        // instead would darken the others and tint the frame). Up to 1.5 keeps the output under lowp's 2.
        _MaxLift        ("Max Lift",        Range(1, 1.5)) = 1.02
        // The balloon body's night re-grade, the brightest surface under the overlay, so the cap above is
        // measured against it. Copied by hand from DiffuseSprite, ShineRotation, ShineRotationTough and
        // ToughBalloonMaterial (the overlay's material is built from these defaults): retune them together.
        _AmbientHue     ("Balloon Ambient Hue",     Range(0, 1)) = 0.25
        _AmbientDimming ("Balloon Ambient Dimming", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend DstColor SrcColor

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"
            #include "../Include/Ground.cginc"

            sampler2D _LightTex;
            fixed4 _ShadowTint;
            float  _ShadowStrength;
            float  _BounceStrength;
            float  _MagnitudeRef;
            fixed4 _AmbientColor;
            half   _MaxLift;
            float  _AmbientHue;
            float  _AmbientDimming;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 gathered = tex2D(_LightTex, IN.texcoord);

                // Local light magnitude from the field at this fragment's world position
                // (overlay UV mapped through the field bounds). Field-off the helper returns
                // the flat global intensity everywhere. This is where the light's intensity
                // couples in per-fragment — the service no longer folds it into the strengths.
                float2 worldPos = _SceneLightFieldBoundsMin.xy + IN.texcoord * _SceneLightFieldBoundsSize.xy;
                float magnitude = SceneLightMagnitudeAt(worldPos);

                // Shadow scales by RELATIVE magnitude (local / reference): field-off this is
                // forced to 1 so the authored shadow strength is untouched — bit-identical —
                // while field-on a brighter patch deepens the shadow and a dim one lifts it.
                float relative = _SceneLightFieldOn < 0.5 ? 1.0 : (magnitude / _MagnitudeRef);
                float shadow = saturate(gathered.a * _ShadowStrength * relative);
                fixed3 shadowTint = lerp(fixed3(1, 1, 1), _ShadowTint.rgb, shadow);

                // Bounce is the scene color down-light measured against the ambient sky:
                // flat sky (bounce == ambient) contributes zero, a bright sprite pushes
                // positive (brightens neighbours in its hue), a dark/black sprite pushes
                // negative (absorbs — darkens neighbours). 0.5 * tint is blend-neutral.
                // Scaled by the ABSOLUTE local magnitude: field-off that equals the global
                // intensity, reproducing the old CPU-side "_bounceStrength * intensity".
                // On a ground level the backdrop the capture sees is the meadow, not the sky, so the bounce is
                // measured against the meadow's own lit colour: open grass nets to neutral and a balloon bleeds
                // onto the grass by how it differs from the grass, as one does onto open sky.
                fixed3 ambient = lerp(_AmbientColor.rgb, _GroundBounceReference.rgb * SceneLightTint(),
                    GroundCoverageAt(worldPos));
                fixed3 bounce = (gathered.rgb - ambient) * _BounceStrength * magnitude;
                // The brightest a balloon is lit here: the key light re-graded as the balloons are, plus the local
                // boost at full strength (a palette light's channels don't pass 1). Balloons cap at white, so 1 at most.
                float3 balloonLight = SceneLightTint() + SceneLightAmbientRegrade(_AmbientHue, _AmbientDimming);
                float local = magnitude - SceneLightAmbientMagnitude();
                float brightest = min(1.0, max(balloonLight.r, max(balloonLight.g, balloonLight.b)) + local);
                fixed3 color = min(shadowTint * 0.5 + bounce, 0.5 * _MaxLift / max(brightest, 0.5));

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}
