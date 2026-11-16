Shader "BalloonParty/Sprite/NukeBlast"
{
    // The nuke's blast (PLAN-NukeBalloon): the disc that grows from the nuke's pop across the board and pops every
    // balloon it reaches. NukeBlastView draws it on a quad centred on the pop and sized to the disc each frame, and
    // pushes the disc's radius and its progress (0 at the pop, 1 when it covers the board). A white-hot core
    // burns at the centre and fades fast; an orange shock ring rides the front, ragged by angle so it reads as
    // fire, not a clean line; behind it the ground is scorched dark, thinning as the disc grows. Over all of it a
    // white flood blinds the board the disc covers, past white so it blooms, fading as the disc slows. The whole
    // disc fades out over its last stretch, so it's gone by the time the view hides it.

    Properties
    {
        [Header(Shock Ring)]
        // World width of the ring behind the disc's front.
        _RingWidth  ("Width", Range(0.05, 2)) = 0.45
        _RingHot    ("Hot Colour", Color) = (1.00, 0.92, 0.55, 1)
        _RingCool   ("Cool Colour", Color) = (1.00, 0.36, 0.06, 1)
        _RingAlpha  ("Strength", Range(0, 1)) = 0.95
        // How ragged the front is, in world units, and how many flames go round it.
        _Ragged     ("Ragged", Range(0, 0.5)) = 0.12
        [IntRange] _Flames ("Flames", Range(3, 48)) = 22

        [Header(Core)]
        // World radius of the white-hot core at the pop, and how fast it fades with the progress.
        _CoreRadius ("Radius", Range(0.1, 4)) = 1.4
        _CoreColour ("Colour", Color) = (1.00, 0.98, 0.90, 1)
        _CoreFade   ("Gone By Progress", Range(0.05, 1)) = 0.35

        [Header(Scorch)]
        _Scorch      ("Colour", Color) = (0.10, 0.05, 0.03, 1)
        _ScorchAlpha ("Strength", Range(0, 1)) = 0.35

        [Header(Flood)]
        _FloodColour ("Colour", Color) = (1.00, 0.98, 0.92, 1)
        // Past white, so it blooms (PLAN-HDR).
        _FloodBoost  ("HDR Boost", Range(1, 8)) = 3
        _FloodAlpha  ("Strength", Range(0, 1)) = 0.9
        // The progress by which the flood has faded away.
        _FloodGone   ("Gone By Progress", Range(0.1, 1)) = 0.8

        [Header(Fade)]
        // The progress from which the whole disc fades out.
        _FadeFrom   ("Fade From", Range(0, 1)) = 0.75

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

            float  _RingWidth;
            fixed4 _RingHot;
            fixed4 _RingCool;
            float  _RingAlpha;
            float  _Ragged;
            float  _Flames;
            float  _CoreRadius;
            fixed4 _CoreColour;
            float  _CoreFade;
            fixed4 _Scorch;
            float  _ScorchAlpha;
            fixed4 _FloodColour;
            float  _FloodBoost;
            float  _FloodAlpha;
            float  _FloodGone;
            float  _FadeFrom;
            float  _Radius;
            float  _Progress;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.offset = mul(unity_ObjectToWorld, IN.vertex).xy
                           - mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                return OUT;
            }

            // Flames round the front: two sines of the angle, drifting with time, 0 to 1.
            float Flames(float angle)
            {
                float flames = floor(_Flames);
                float a = sin(angle * flames + _Time.y * 3.0);
                float b = sin(angle * (flames * 0.5 + 1.0) - _Time.y * 2.1);
                return saturate(0.5 + 0.3 * a + 0.2 * b);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float d = length(IN.offset);
                float radius = max(_Radius, 1e-4);
                float aa = max(fwidth(d), 1e-4);

                // The front, ragged by angle so it burns rather than draws a line.
                float front = radius - _Ragged * Flames(atan2(IN.offset.y, IN.offset.x));
                float inside = 1.0 - smoothstep(front - aa, front + aa, d);
                if (inside <= 0.0)
                {
                    discard;
                }

                // The ring brightest at the front, hot at its leading edge and cooling behind.
                float behind = front - d;
                float ring = saturate(1.0 - behind / _RingWidth);
                fixed3 ringColour = lerp(_RingCool.rgb, _RingHot.rgb, ring * ring);
                ring *= ring;

                // The scorched ground behind the ring, thinning as the disc grows.
                float scorch = saturate(behind / _RingWidth) * _ScorchAlpha * (1.0 - _Progress);

                // The core at the pop, gone early.
                float core = saturate(1.0 - d / _CoreRadius) * saturate(1.0 - _Progress / _CoreFade);
                core *= core;

                float fade = 1.0 - smoothstep(_FadeFrom, 1.0, _Progress);
                float ringAlpha = ring * _RingAlpha;
                float flood = pow(saturate(1.0 - _Progress / _FloodGone), 1.5) * _FloodAlpha;
                // Premultiplied: the ring, core and flood add light, the scorch only darkens what is under it.
                fixed3 colour = ringColour * ringAlpha + _CoreColour.rgb * core + _Scorch.rgb * scorch * (1.0 - ringAlpha)
                              + _FloodColour.rgb * _FloodBoost * flood;
                float alpha = saturate(ringAlpha + core + scorch + flood);
                return fixed4(colour * inside * fade, alpha * inside * fade);
            }
            ENDCG
        }
    }
}
