Shader "BalloonParty/Sprite/TrailLightDriven"
{
    // A Unity TrailRenderer ribbon lit by the scene light field (SceneLight.cginc): the flat strip is
    // shaded as if it were a rounded tube, and the field's LOCAL toward-light direction decides which
    // side of the tube is lit and which falls into self-shadow. A coloured local light sweeping past the
    // trail rolls the shaded side around it and tints the lit face. Field-off falls back to the flat
    // ambient direction — still a consistent rounded look, just no local bending.
    //
    // Cross-section normal: TrailRenderer's V (texcoord.y) runs 0..1 ACROSS the ribbon width, so
    // rib = V*2-1 is the signed distance from the centre ridge to each edge. The ribbon's world across-
    // axis is reconstructed per-fragment from screen-space derivatives of worldPos vs. V (no per-vertex
    // tangent needed, so this works with the stock TrailRenderer mesh). dot(acrossAxis, toLight)·rib is
    // then +1 on the edge facing the light, -1 on the far edge, 0 at the ridge.
    Properties
    {
        [PerRendererData] _MainTex ("Trail Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)

        [Header(Self Shadow)]
        // How dark the shadowed side of the tube goes (0 = flat/no self-shadow, 1 = to _ShadowFloor).
        _ShadowStrength ("Self-Shadow Strength", Range(0, 1)) = 0.6
        // Multiplier the fully-shadowed edge reaches (0 = black, 1 = no darkening).
        _ShadowFloor ("Shadow Floor", Range(0, 1)) = 0.35
        // Cross-section curvature: 1 = linear tube, higher = a tighter lit highlight / broader shadow.
        _Roundness ("Cross-Section Roundness", Range(0.25, 4)) = 1.4
        // More scene light = deeper self-shadow contrast; scales the light magnitude into the 0..1 gate.
        _LightContrast ("Light Contrast", Range(0, 4)) = 1.5

        [Header(Light Colour)]
        // How strongly a local light's palette colour tints the lit face (hue only — brightness is the
        // self-shadow's job, so the tint is luminance-normalized to avoid double-exposing).
        _LightResponse ("Light Colour Response", Range(0, 1)) = 0.5
        // Additive bloom from nearby LOCAL lights only (0 at rest); paints the trail with a passing
        // spark/laser's colour without touching the ambient look.
        _LocalGlow ("Local Light Glow", Range(0, 2)) = 0.3
        // Bright rim on the lit edge — a wet-highlight streak down the tube's crest.
        _RimStrength ("Lit-Edge Rim", Range(0, 2)) = 0.4

        [Header(Silhouette)]
        // Soft alpha rolloff at the ribbon's two long edges, so the strip reads as a rounded tube
        // rather than a hard-edged band. 0 = square edges.
        _EdgeSoftness ("Edge Softness", Range(0, 0.5)) = 0.25
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
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

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
                float2 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float _ShadowStrength;
            float _ShadowFloor;
            float _Roundness;
            float _LightContrast;
            float _LightResponse;
            float _LocalGlow;
            float _RimStrength;
            float _EdgeSoftness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.color    = IN.color * _Color;
                OUT.texcoord = TRANSFORM_TEX(IN.texcoord, _MainTex);
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 worldPos = IN.worldPos;
                float v = IN.texcoord.y;

                // World-space direction of increasing V (across the ribbon), reconstructed from the
                // screen-space correlation of worldPos with V — degenerates to zero at the ribbon tip,
                // where the neutral 0.5 illumination below leaves the tube unshaded (the tip is usually
                // fading out on alpha anyway).
                float2 across = ddx(worldPos) * ddx(v) + ddy(worldPos) * ddy(v);
                float acrossLen = length(across);
                float2 acrossN = acrossLen > 1e-6 ? across / acrossLen : float2(0.0, 0.0);

                // Signed cross-section: +1 on the edge facing the light, -1 on the far edge, 0 at ridge.
                float rib = v * 2.0 - 1.0;
                float2 toLight = SceneLightDirectionAt(worldPos);
                float facing = dot(acrossN, toLight) * rib;
                float lightSide = pow(saturate(0.5 + 0.5 * facing), _Roundness);

                // No light, no self-shadow (the family's ShadowLightFade convention); brighter light
                // deepens the contrast. ShadowLightFadeAt is already 1.0 before the owner has pushed.
                float lightMag = SceneLightMagnitudeAt(worldPos);
                float shadowGate = ShadowLightFadeAt(worldPos) * saturate(lightMag * _LightContrast);
                float shadeMul = lerp(1.0, _ShadowFloor, (1.0 - lightSide) * _ShadowStrength * shadowGate);

                fixed4 tex = tex2D(_MainTex, IN.texcoord);
                float3 rgb = IN.color.rgb * tex.rgb * shadeMul;

                // Hue-only tint from the field (luminance-normalized so the self-shadow keeps ownership
                // of brightness), plus additive local-light bloom and a lit-edge rim.
                float3 tint = SceneLightTintAt(worldPos);
                float tintLum = max(tint.r, max(tint.g, tint.b));
                float3 tintHue = tintLum > 1e-4 ? tint / tintLum : float3(1.0, 1.0, 1.0);
                rgb *= lerp(float3(1.0, 1.0, 1.0), tintHue, _LightResponse);
                rgb += SceneLightLocalAt(worldPos) * (_LocalGlow * lightSide);
                rgb += tintHue * (_RimStrength * pow(lightSide, 3.0) * shadowGate);

                float edge = smoothstep(0.0, _EdgeSoftness, v) * smoothstep(0.0, _EdgeSoftness, 1.0 - v);
                float alpha = IN.color.a * tex.a * edge;

                return fixed4(rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
