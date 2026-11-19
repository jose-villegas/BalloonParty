Shader "BalloonParty/Particle/SporeSpeck"
{
    // A meadow mushroom's spore (PLAN-MeadowMushrooms, "A puff of spores"): a small soft round dot, drawn from the
    // particle's own quad with no texture read, in the particle's colour. By day it is coloured dust: its
    // brightness follows the time of day's light, so it is plain at noon and dims toward dusk. At night it
    // glows a little past white instead, as the cap's warts do, by the same measure of how dark the time of day
    // is (SceneLightDesaturate's), worked out once a vertex from a global. Nothing is sampled per pixel.
    Properties
    {
        // How far in from its edge a speck's soft rim reaches, as a share of its radius.
        _Softness ("Soft Rim", Range(0.05, 1)) = 0.6
        // How much of the time of day's dimming the dust takes: 0 keeps it as bright at dusk as at noon.
        _LightInfluence ("Light Influence", Range(0, 1)) = 0.6

        [Header(Night Glow)]
        // How far past white a speck shines at full night, and how dark the time of day's light has to be for
        // the glow to start and to be full (the cap's warts use the same two).
        // By day too a speck is a little brighter than its colour, so it stands off the grass and blooms faintly.
        _Gain ("Brightness (HDR)", Range(0.5, 3)) = 1.4
        _NightGlow ("Night Glow (HDR)", Range(0, 4)) = 0.8
        _NightGlowFrom ("Glow Starts at Dark", Range(0, 1)) = 0.25
        _NightGlowFull ("Glow Full at Dark", Range(0, 1)) = 0.6
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
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // rgb the speck's colour as lit or glowing; a its opacity.
                half4  color  : COLOR;
                // From the speck's middle, 1 at the quad's edge.
                float2 fromMiddle : TEXCOORD0;
            };

            float _Softness;
            float _LightInfluence;
            float _Gain;
            float _NightGlow;
            float _NightGlowFrom;
            float _NightGlowFull;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.fromMiddle = IN.uv * 2.0 - 1.0;

                // The time of day's light alone, with no light of the board's in it: a blast beside a speck
                // neither bleaches it nor puts its glow out.
                float bright = saturate(dot(SceneLightTint(), float3(0.2126, 0.7152, 0.0722)));
                float glow = _NightGlow * smoothstep(_NightGlowFrom, _NightGlowFull, 1.0 - bright);
                OUT.color = half4(IN.color.rgb * (lerp(1.0, bright, _LightInfluence) * _Gain + glow), IN.color.a);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float rim = length(IN.fromMiddle);
                float cover = saturate((1.0 - rim) / max(_Softness, 1e-3)) * IN.color.a;
                return half4(IN.color.rgb * cover, cover);
            }
            ENDCG
        }
    }
}
