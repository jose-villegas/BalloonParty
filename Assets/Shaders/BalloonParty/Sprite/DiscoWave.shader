Shader "BalloonParty/Sprite/DiscoWave"
{
    // The disco ball's wave (PLAN-DiscoBalloon): the disc that grows from the ball's pop across the board and
    // paints it rainbow. DiscoWaveView draws it on a quad centred on the pop and sized to the disc each frame, and
    // pushes the disc's radius and its progress (0 at the pop, 1 when it covers the board). A bright edge band
    // runs through the level's own colours by angle (Include/RainbowBand.cginc, the same bands as the rainbow
    // balloon's body), turning slowly, with a soft wash behind it that thins as the disc grows. The whole disc
    // fades out over its last stretch, so it's gone by the time the view hides it.

    Properties
    {
        [Header(Edge)]
        // World width of the band behind the disc's front.
        _EdgeWidth     ("Width", Range(0.05, 2)) = 0.35
        _EdgeAlpha     ("Strength", Range(0, 1)) = 0.85
        // How many times the level's colours go round the disc; whole, so they meet across the seam.
        [IntRange] _EdgeRepeats ("Colour Repeats", Range(1, 8)) = 2
        _EdgeSoftness  ("Colour Softness", Range(0, 1)) = 0.6
        // Turns per second.
        _EdgeTurn      ("Turn Speed", Range(-1, 1)) = 0.15

        [Header(Wash)]
        _WashAlpha     ("Strength", Range(0, 1)) = 0.22
        // How far the wash reaches in from the edge, as a share of the radius.
        _WashDepth     ("Depth", Range(0.05, 1)) = 0.6

        [Header(Fade)]
        // The progress from which the whole disc fades out.
        _FadeFrom      ("Fade From", Range(0, 1)) = 0.7

        [PerRendererData] _Radius   ("Radius", Float) = 0
        [PerRendererData] _Progress ("Progress", Float) = 0
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

            #include "UnityCG.cginc"
            #include "../Include/MathConst.cginc"
            #include "../Include/RainbowBand.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                // The fragment's offset from the disc's centre, in world units.
                float2 offset : TEXCOORD0;
            };

            float _EdgeWidth;
            float _EdgeAlpha;
            float _EdgeRepeats;
            float _EdgeSoftness;
            float _EdgeTurn;
            float _WashAlpha;
            float _WashDepth;
            float _FadeFrom;
            float _Radius;
            float _Progress;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.offset = mul(unity_ObjectToWorld, IN.vertex).xy
                           - mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float d = length(IN.offset);
                float radius = max(_Radius, 1e-4);
                float aa = max(fwidth(d), 1e-4);

                // Inside the disc, a crisp front.
                float inside = 1.0 - smoothstep(radius - aa, radius + aa, d);
                if (inside <= 0.0)
                {
                    discard;
                }

                // The band brightest at the front, falling off behind it.
                float behind = radius - d;
                float edge = saturate(1.0 - behind / _EdgeWidth);
                edge *= edge;

                // The wash, thinning toward the centre and as the disc grows.
                float wash = saturate(1.0 - behind / (radius * _WashDepth)) * _WashAlpha * (1.0 - _Progress);

                float angle = atan2(IN.offset.y, IN.offset.x);
                float band = (angle / BP_TAU + _Time.y * _EdgeTurn) * floor(_EdgeRepeats) * max(_RainbowBandCount, 1.0);
                fixed3 colour = RainbowBandColor(band, _EdgeSoftness);

                float fade = 1.0 - smoothstep(_FadeFrom, 1.0, _Progress);
                float alpha = saturate(edge * _EdgeAlpha + wash) * inside * fade;
                return fixed4(colour * alpha, alpha);
            }
            ENDCG
        }
    }
}
