Shader "BalloonParty/Scenario/SpeckField"
{
    // Renders the GPU speck buffer as camera-facing quads (one per speck), expanded in the vertex shader
    // from SV_VertexID — no per-frame CPU. SpeckField.cs draws it through a MeshRenderer holding a dummy
    // count*6-vertex mesh (so the field sorts with sprites); each vert is repositioned here from _Specks.
    Properties
    {
        _MainTex   ("Speck Sprite", 2D)              = "white" {}
        _Color     ("Tint",         Color)           = (1, 1, 1, 0.5)
        _SpeckSize ("Speck Size",   Float)           = 0.03
        _MinScale  ("Min Scale",    Float)           = 0.5
        _MaxScale  ("Max Scale",    Float)           = 1.5
        _ScalePulseSpeed ("Scale Pulse Speed (min,max)", Vector) = (0.4, 1.0, 0, 0)
        _TrailLength ("Trail Length (per speed)", Float) = 0
        _TrailMax    ("Trail Max Length", Float)         = 0.5
        _FadeIn    ("Fade In (life frac)",  Range(0, 0.5)) = 0.15
        _FadeOut   ("Fade Out (life frac)", Range(0, 0.5)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #include "UnityCG.cginc"

            struct Speck
            {
                float2 position;
                float2 velocity;
                float seed;
                float age;
                float lifetime;
                float2 effectiveVel;
            };

            StructuredBuffer<Speck> _Specks;
            sampler2D _MainTex;
            fixed4 _Color;
            float _SpeckSize;
            float _MinScale;
            float _MaxScale;
            float4 _ScalePulseSpeed;
            float _SpeckTime;
            float _FadeIn;
            float _FadeOut;
            float _TrailLength;
            float _TrailMax;

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float  alpha : TEXCOORD1;
            };

            // Two triangles → a unit quad centered on the origin, in [-0.5, 0.5].
            static const float2 Corners[6] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
                float2(-0.5, -0.5), float2(0.5, 0.5), float2(-0.5, 0.5)
            };

            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            v2f vert(uint vid : SV_VertexID)
            {
                // The mesh has 6 verts per speck; split the id into speck index + quad corner.
                Speck s = _Specks[vid / 6u];
                float2 corner = Corners[vid % 6u];

                // Life fade: ramp in over _FadeIn, out over _FadeOut (fractions of lifetime), driving both
                // alpha and scale so specks pop in and shrink away.
                float lifeT = s.lifetime > 0.0 ? saturate(s.age / s.lifetime) : 1.0;
                float fadeIn = _FadeIn > 0.0 ? saturate(lifeT / _FadeIn) : 1.0;
                float fadeOut = _FadeOut > 0.0 ? saturate((1.0 - lifeT) / _FadeOut) : 1.0;
                float fade = fadeIn * fadeOut;

                // Scale oscillation over time — some specks grow while others shrink, faking gentle drift
                // toward/away from the camera; the scale analog of the Brownian motion. Per-speck phase
                // AND rate (both seed-derived) so they desync in size and speed.
                float pulseSpeed = lerp(_ScalePulseSpeed.x, _ScalePulseSpeed.y, hash11(s.seed * 91.7));
                float scaleT = 0.5 + 0.5 * sin(_SpeckTime * pulseSpeed + s.seed * 6.2831853);
                float baseScale = lerp(_MinScale, _MaxScale, scaleT);
                float size = _SpeckSize * baseScale * fade;

                // Trail: stretch the quad along the speck's motion, scaled by speed (capped by _TrailMax).
                // Below a tiny speed it stays a round dot (velocity direction is undefined there).
                float speed = length(s.effectiveVel);
                float2 along = float2(0.0, 1.0);
                float length2 = size;
                if (speed > 1e-4 && _TrailLength > 0.0)
                {
                    along = s.effectiveVel / speed;
                    length2 = size + min(speed * _TrailLength, _TrailMax);
                }
                float2 across = float2(along.y, -along.x);

                // 2D game on the XY plane — offset in the velocity-aligned frame (round when not trailing).
                float2 offset = across * (corner.x * size) + along * (corner.y * length2);
                float3 world = float3(s.position + offset, 0.0);

                v2f o;
                o.pos = UnityWorldToClipPos(world);
                o.uv = corner + 0.5;
                o.alpha = fade;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                col.a *= i.alpha;
                return col;
            }
            ENDCG
        }
    }
}
