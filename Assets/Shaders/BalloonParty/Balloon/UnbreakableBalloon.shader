Shader "BalloonParty/Balloon/UnbreakableBalloon"
{
    // Chrome sprite shader for the Unbreakable balloon.
    // Grabs the scene behind the sprite and samples it with convex-mirror
    // distortion (sphere-normal based) for realtime 2D reflections. All four
    // quadrant sprites share a _SphereCenter (world pos via MPB) so the
    // reflection and specular highlight are coherent across the composed
    // sphere. Also adds a radial metallic gradient, a specular highlight,
    // a traveling chrome rim, periodic shine, and deflect flash.

    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Sphere)]
        [HideInInspector] _SphereCenter ("Sphere Center", Vector) = (0, 0, 0, 0)
        _SphereRadius ("Sphere Radius", Float) = 0.5

        [Header(Reflection)]
        _ReflectionStrength ("Strength",        Range(0, 1))       = 0.4
        _ReflectionSpread   ("Spread",          Range(0.01, 1.0))  = 0.15
        _ReflectionFresnel  ("Fresnel Power",   Range(0.5, 5.0))   = 1.5

        [Header(Metallic Shading)]
        _MetalCenterColor   ("Center Tint",      Color)             = (0.88, 0.90, 0.95, 1)
        _MetalEdgeColor     ("Edge Tint",        Color)             = (0.10, 0.10, 0.14, 1)
        _MetalFalloff       ("Falloff",          Range(0.5, 4.0))   = 1.8
        _MetalDetailStrength("Detail Strength",  Range(0.1, 1.0))   = 0.5

        [Header(Specular Highlight)]
        _SpecularPos       ("Position (sphere XY)", Vector)            = (-0.3, 0.35, 0, 0)
        _SpecularSize      ("Size",                 Range(0.05, 1.5))  = 0.45
        _SpecularIntensity ("Intensity",            Range(0, 3))       = 1.5
        _SpecularSharpness ("Sharpness",            Range(0.5, 8))     = 2.0
        _SpecularStretch   ("Aniso Stretch",        Range(1, 10))      = 4.0
        _SpecularAngle     ("Aniso Angle (deg)",    Range(0, 180))     = 135
        _SpecularBend      ("Aniso Bend",           Range(-3, 3))      = -1.0
        _SpecularColor     ("Color",                Color)             = (1, 1, 1, 1)

        [Header(Chrome Rim)]
        _RimColor       ("Color",           Color)             = (0.7, 0.75, 0.85, 1.0)
        _RimWidth       ("Edge Width",      Range(0, 0.06))    = 0.02
        _RimIntensity   ("Intensity",       Range(0, 2))       = 0.6

        [Header(Rim Sweep)]
        _RimSweepColor  ("Color",           Color)             = (0.95, 0.97, 1.0, 1.0)
        _RimSweepIntensity("Intensity",     Range(0, 3))       = 1.2
        _RimSweepSpeed  ("Speed",           Range(0.1, 3.0))   = 0.6
        _RimSweepWidth  ("Arc Width",       Range(0.05, 0.5))  = 0.2

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

        // Capture everything rendered so far (background, other balloons).
        // Named texture so all Unbreakable sprites share one grab per frame.
        GrabPass { "_GrabTexture" }

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
                float4 vertex     : SV_POSITION;
                fixed4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float4 grabPos    : TEXCOORD1;
                float2 worldPos   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_ST;
            float4    _MainTex_TexelSize;

            sampler2D _GrabTexture;

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;

            // Sphere (shared)
            float4 _SphereCenter;
            float  _SphereRadius;

            // Reflection
            float  _ReflectionStrength;
            float  _ReflectionSpread;
            float  _ReflectionFresnel;

            // Metallic shading
            fixed4 _MetalCenterColor;
            fixed4 _MetalEdgeColor;
            float  _MetalFalloff;
            float  _MetalDetailStrength;

            // Specular highlight
            float4 _SpecularPos;
            float  _SpecularSize;
            float  _SpecularIntensity;
            float  _SpecularSharpness;
            float  _SpecularStretch;
            float  _SpecularAngle;
            float  _SpecularBend;
            fixed4 _SpecularColor;

            // Chrome rim (static)
            fixed4 _RimColor;
            float  _RimWidth;
            float  _RimIntensity;

            // Rim sweep (animated)
            fixed4 _RimSweepColor;
            float  _RimSweepIntensity;
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

                // World position for sphere-local calculations
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;

                // Screen-space position for GrabPass sampling
                OUT.grabPos = ComputeGrabScreenPos(OUT.vertex);

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
            // Rim sweep: angular gradient that travels around the edge,
            // simulating a light rotating around the metallic surface.
            // Returns 0..1 gradient strength (like the diagonal shine).
            // ----------------------------------------------------------------
            float RimSweep(float2 spherePos, float time)
            {
                float angle = atan2(spherePos.y, spherePos.x);
                float normAngle = angle / (2.0 * UNITY_PI) + 0.5;

                float sweep = frac(time * _RimSweepSpeed);

                float dist = abs(normAngle - sweep);
                dist = min(dist, 1.0 - dist);

                return smoothstep(_RimSweepWidth, 0.0, dist);
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

                // Sphere-local position: pixel's offset from sphere center,
                // normalized by radius. Consistent across all 4 rotated quadrants.
                // Ranges from ~(-1,-1) to ~(1,1) across the full composed sphere.
                float2 spherePos = (IN.worldPos - _SphereCenter.xy) / max(_SphereRadius, 0.001);
                float  sphereDist = saturate(length(spherePos));

                // ---- Metallic radial gradient (replaces sprite RGB) ----
                // The sprite provides masking (alpha) and the bolt-pattern
                // detail via its luminance. The gradient itself controls the
                // full brightness range — bright silver at centre, dark at rim.
                if (alpha > 0.01)
                {
                    float detail = dot(sprite.rgb, float3(0.299, 0.587, 0.114));
                    float grad = pow(sphereDist, _MetalFalloff);
                    fixed3 metallic = lerp(_MetalCenterColor.rgb, _MetalEdgeColor.rgb, grad);
                    sprite.rgb = metallic * smoothstep(0.0, _MetalDetailStrength, detail);
                }

                // ---- Realtime reflection (convex mirror from GrabPass) ----
                if (alpha > 0.01 && _ReflectionStrength > 0.001)
                {
                    float2 grabUV = IN.grabPos.xy / IN.grabPos.w;

                    // Sample outward from pixel position along the sphere
                    // normal direction. On a convex mirror each point
                    // reflects what is to its side — center sees behind,
                    // edges see sideways. Spread scales how far out we
                    // reach into the surrounding scene.
                    float2 reflUV = grabUV + spherePos * _ReflectionSpread;
                    reflUV = saturate(reflUV);

                    fixed3 reflected = tex2D(_GrabTexture, reflUV).rgb;

                    // Fresnel blend: edge-heavy by default (chrome reflects
                    // more at glancing angles), tuneable via power slider
                    float fresnel = pow(sphereDist, _ReflectionFresnel);
                    float reflMask = lerp(0.15, 1.0, fresnel) * _ReflectionStrength;

                    sprite.rgb = lerp(sprite.rgb, reflected, reflMask * alpha);
                }

                // ---- Specular highlight (anisotropic curved streak) ----
                if (alpha > 0.01 && _SpecularIntensity > 0.001)
                {
                    float2 d = spherePos - _SpecularPos.xy;

                    // Rotate into stretch-axis frame
                    float rad = _SpecularAngle * (UNITY_PI / 180.0);
                    float cs = cos(rad);
                    float sn = sin(rad);
                    float along = d.x * cs + d.y * sn;
                    float across = -d.x * sn + d.y * cs;

                    // Circular-arc centerline: the streak follows an arc
                    // whose curvature matches the sphere. Bend=0 is straight,
                    // negative/positive curves toward/away from sphere center.
                    // arcRadius = 1/bend; offset = R - sqrt(R²-along²)
                    // This naturally wraps tighter at the ends.
                    float curveCenter = 0.0;
                    float absBend = abs(_SpecularBend);
                    if (absBend > 0.01)
                    {
                        float R = 1.0 / absBend;
                        float clamped = clamp(along, -R, R);
                        curveCenter = sign(_SpecularBend) * (R - sqrt(max(R * R - clamped * clamped, 0.0)));
                    }
                    float perpDist = across - curveCenter;

                    // Gaussian falloff: produces smooth elliptical highlights.
                    // Stretch widens along-axis relative to across-axis.
                    float sigmaAlong  = max(_SpecularSize, 0.001);
                    float sigmaAcross = sigmaAlong / max(_SpecularStretch, 1.0);
                    float g = exp(-0.5 * (along * along / (sigmaAlong * sigmaAlong)
                                        + perpDist * perpDist / (sigmaAcross * sigmaAcross)));
                    float spec = pow(g, _SpecularSharpness) * _SpecularIntensity;
                    sprite.rgb += _SpecularColor.rgb * spec * alpha;
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

                // ---- Chrome rim (static + animated sweep) ----
                if (alpha > 0.01 && _RimWidth > 0.0)
                {
                    float edge = EdgeMask(spriteUV, _RimWidth);

                    // Static rim — always visible, outlines the sphere edge
                    sprite.rgb += _RimColor.rgb * edge * _RimIntensity * alpha;

                    // Animated sweep — gradient highlight that rotates on
                    // top of the static rim, using a separate color
                    if (_RimSweepIntensity > 0.001)
                    {
                        float sweep = RimSweep(spherePos, time);
                        sprite.rgb += _RimSweepColor.rgb * edge * sweep * _RimSweepIntensity * alpha;
                    }
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

