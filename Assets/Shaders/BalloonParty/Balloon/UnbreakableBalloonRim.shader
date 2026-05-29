Shader "BalloonParty/Balloon/UnbreakableBalloonRim"
{
    // Lightweight chrome-rim shader for the Unbreakable balloon's ring
    // sprites (outer and inner circles). Renders a static metallic rim
    // on the sprite's alpha edge plus an animated sweep highlight that
    // rotates around it.
    //
    // Sphere position is derived from the sprite UV (center = 0,0) so
    // no MaterialPropertyBlock push is required — GPU instancing works
    // for both rings sharing the same material.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Chrome Rim)]
        _RimColor     ("Color",      Color)           = (0.7, 0.75, 0.85, 1.0)
        _RimWidth     ("Edge Width", Range(0, 0.06))  = 0.02
        _RimIntensity ("Intensity",  Range(0, 2))     = 0.6

        [Header(Rim Sweep)]
        _RimSweepColor    ("Color",     Color)             = (0.95, 0.97, 1.0, 1.0)
        _RimSweepIntensity("Intensity", Range(0, 3))       = 1.2
        _RimSweepSpeed    ("Speed",     Range(0.1, 3.0))   = 0.6
        _RimSweepWidth    ("Arc Width", Range(0.05, 0.5))  = 0.2

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
        Blend    SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "Default"

            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target   3.0
            #pragma multi_compile _ PIXELSNAP_ON
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct Attributes
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color  : COLOR;
                float2 uv     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;

            // Chrome rim
            fixed4 _RimColor;
            float  _RimWidth;
            float  _RimIntensity;

            // Rim sweep
            fixed4 _RimSweepColor;
            float  _RimSweepIntensity;
            float  _RimSweepSpeed;
            float  _RimSweepWidth;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv     = IN.uv;
                OUT.color  = IN.color * _Color * _RendererColor;

                #ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
                #endif
                return OUT;
            }

            // ----------------------------------------------------------------
            // Alpha sampling with bounds guard
            // ----------------------------------------------------------------
            inline fixed SampleAlpha(float2 uv)
            {
                float2 inB = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inB.x * inB.y;
            }

            // ----------------------------------------------------------------
            // Edge detection — 8-tap alpha-edge mask
            // ----------------------------------------------------------------
            float EdgeMask(float2 uv, float width)
            {
                float center = SampleAlpha(uv);
                if (center < 0.01)
                {
                    return 0;
                }

                float minNeighbour = 1.0;

                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0)
                        {
                            continue;
                        }
                        float2 offset = float2(x, y) * width;
                        float a = SampleAlpha(uv + offset);
                        minNeighbour = min(minNeighbour, a);
                    }
                }

                return saturate((center - minNeighbour) / max(center, 0.001));
            }

            // ----------------------------------------------------------------
            fixed4 frag(Varyings IN) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, IN.uv) * IN.color;
                float alpha = col.a;

                if (alpha < 0.01 || _RimWidth <= 0.0)
                {
                    return col;
                }

                float edge = EdgeMask(IN.uv, _RimWidth);

                // Static rim
                col.rgb += _RimColor.rgb * edge * _RimIntensity * alpha;

                // Animated sweep — derive angular position from UV center
                if (_RimSweepIntensity > 0.001 && edge > 0.001)
                {
                    float2 spherePos = (IN.uv - 0.5) * 2.0;
                    float angle = atan2(spherePos.y, spherePos.x);
                    float normAngle = angle / (2.0 * UNITY_PI) + 0.5;
                    float sweep = frac(_Time.y * _RimSweepSpeed);
                    float dist = abs(normAngle - sweep);
                    dist = min(dist, 1.0 - dist);
                    float sweepMask = smoothstep(_RimSweepWidth, 0.0, dist);

                    col.rgb += _RimSweepColor.rgb * edge * sweepMask * _RimSweepIntensity * alpha;
                }

                return col;
            }
            ENDCG
        }
    }
}

