Shader "BalloonParty/Balloon/UnbreakableBalloon"
{
    // Chrome sprite shader for the Unbreakable balloon.
    // Adds a vertical metallic gradient (top-light / bottom-dark) to give
    // the flat sprite a chrome environment feel, a traveling specular rim
    // that sweeps along the sprite edge, a periodic shine band, and a
    // deflect flash. The sprite carries all panel/seam/lens art — the
    // gradient respects each panel's own baked highlights.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Metallic Shading)]
        _MetalCenterColor ("Center Tint",  Color)             = (1.05, 1.05, 1.1, 1)
        _MetalEdgeColor   ("Edge Tint",    Color)             = (0.55, 0.55, 0.60, 1)
        _MetalFalloff     ("Falloff",      Range(0.5, 4.0))   = 1.5

        [Header(Chrome Rim)]
        _RimColor       ("Color",           Color)             = (0.85, 0.90, 1.0, 1.0)
        _RimWidth       ("Edge Width",      Range(0.003, 0.06)) = 0.02
        _RimIntensity   ("Intensity",       Range(0, 2))       = 1.0
        _RimSweepSpeed  ("Sweep Speed",     Range(0.1, 3.0))   = 0.6
        _RimSweepWidth  ("Sweep Arc",       Range(0.05, 0.5))  = 0.2

        [Header(Shine)]
        _ShineWidth    ("Width",    Range(0, 0.3))  = 0.08
        _ShineSpeed    ("Speed",    Range(0, 5))    = 0.8
        _ShineInterval ("Interval", Range(0, 10))   = 4.0


        [Header(Deflect Flash)]
        _DeflectFlash ("Flash (0-1)", Range(0, 1)) = 0

        [Header(Sprite)]
        _SpriteScale ("Scale", Range(0.1, 1.0)) = 1.0

        [PerRendererData] _TimeOffset ("Time Offset", Float) = 0
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
            float4    _MainTex_ST;
            float4    _MainTex_TexelSize;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;

            // Metallic shading
            fixed4 _MetalCenterColor;
            fixed4 _MetalEdgeColor;
            float  _MetalFalloff;

            // Chrome rim
            fixed4 _RimColor;
            float  _RimWidth;
            float  _RimIntensity;
            float  _RimSweepSpeed;
            float  _RimSweepWidth;

            // Shine
            float _ShineWidth;
            float _ShineSpeed;
            float _ShineInterval;


            // Deflect
            float _DeflectFlash;


            // Sprite
            float _SpriteScale;
            float _TimeOffset;

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
            // Alpha sampling helpers
            // ----------------------------------------------------------------
            inline fixed SampleAlpha(float2 uv)
            {
                float2 inB = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, uv).a * inB.x * inB.y;
            }

            // ----------------------------------------------------------------
            // Edge detection — returns 0 in interior, 1 at alpha edge.
            // Samples alpha in 8 directions; the difference between the
            // center and the minimum neighbour marks the edge band.
            // ----------------------------------------------------------------
            float EdgeMask(float2 uv, float width)
            {
                float center = SampleAlpha(uv);
                if (center < 0.01) return 0;

                float minNeighbour = 1.0;

                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        float2 offset = float2(x, y) * width;
                        float a = SampleAlpha(uv + offset);
                        minNeighbour = min(minNeighbour, a);
                    }
                }

                // Edge strength: high where a neighbour is transparent
                return saturate((center - minNeighbour) / max(center, 0.001));
            }

            // ----------------------------------------------------------------
            // Chrome rim: angular sweep that highlights different edge
            // sections over time — simulates a light rotating around the
            // metallic surface.
            // ----------------------------------------------------------------
            float ChromeRim(float2 uv, float edge, float time)
            {
                // Angle of this pixel relative to sprite center
                float2 dir = uv - 0.5;
                float angle = atan2(dir.y, dir.x); // -PI..PI
                float normAngle = angle / (2.0 * UNITY_PI) + 0.5; // 0..1

                // Sweep position (0..1, wrapping)
                float sweep = frac(time * _RimSweepSpeed);

                // Distance from sweep center (wrapping around 0/1 boundary)
                float dist = abs(normAngle - sweep);
                dist = min(dist, 1.0 - dist);

                // Smooth falloff within sweep arc
                float highlight = smoothstep(_RimSweepWidth, 0.0, dist);

                return edge * highlight * _RimIntensity;
            }

            // ----------------------------------------------------------------
            fixed4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y + _TimeOffset;

                // Scale sprite UV inward for shadow margin
                float2 spriteUV = (IN.uv - 0.5) / _SpriteScale + 0.5;
                float2 inBounds = step(0.0, spriteUV) * step(spriteUV, 1.0);
                float  spriteMask = inBounds.x * inBounds.y;

                // ---- Main sprite ----
                fixed4 sprite = tex2D(_MainTex, spriteUV) * IN.color;
                sprite.a *= spriteMask;
                float alpha = sprite.a;

                // ---- Metallic radial gradient (center bright, edges dark) ----
                if (alpha > 0.01)
                {
                    float2 fromCenter = (spriteUV - 0.5) * 2.0;
                    float dist = saturate(length(fromCenter));
                    float grad = pow(dist, _MetalFalloff);
                    fixed3 tint = lerp(_MetalCenterColor.rgb, _MetalEdgeColor.rgb, grad);
                    sprite.rgb *= tint;
                }

                // ---- Diagonal shine band (same as SpriteShineShadow) ----
                if (_ShineSpeed > 0 && alpha > 0.01)
                {
                    float sweepDur = 1.0 / max(_ShineSpeed, 0.001);
                    float cycleDur = sweepDur + _ShineInterval;
                    float t = fmod(time, cycleDur);
                    float shineLoc = -_ShineWidth + (1.0 + 2.0 * _ShineWidth) * saturate(t / sweepDur);

                    float projection = (spriteUV.x + spriteUV.y) / 2;
                    float shineDist = abs(projection - shineLoc);
                    float shineStr = saturate(1.0 - shineDist / max(_ShineWidth, 0.001));
                    sprite.rgb += alpha * shineStr * 0.5;
                }

                // ---- Chrome rim ----
                if (alpha > 0.01)
                {
                    float edge = EdgeMask(spriteUV, _RimWidth);
                    float rim  = ChromeRim(spriteUV, edge, time);
                    sprite.rgb += _RimColor.rgb * rim * alpha;
                }


                // ---- Deflect flash ----
                if (_DeflectFlash > 0.001)
                {
                    sprite.rgb += alpha * _DeflectFlash * 0.8;
                }

                return sprite;
            }
            ENDCG
        }
    }
}

