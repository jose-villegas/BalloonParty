Shader "BalloonParty/Scenario/SpeckField"
{
    // Renders the GPU speck buffer as camera-facing quads (one per speck), expanded in the vertex shader
    // from SV_VertexID + SV_InstanceID — no mesh, no per-frame CPU. Driven by SpeckField.cs via
    // Graphics.DrawProceduralNow(Triangles, 6, count). STARTER — validate + tune in-editor.
    Properties
    {
        _MainTex   ("Speck Sprite", 2D)              = "white" {}
        _Color     ("Tint",         Color)           = (1, 1, 1, 0.5)
        _SpeckSize ("Speck Size",   Float)           = 0.03
        _MinScale  ("Min Scale",    Float)           = 0.5
        _MaxScale  ("Max Scale",    Float)           = 1.5
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
            };

            StructuredBuffer<Speck> _Specks;
            sampler2D _MainTex;
            fixed4 _Color;
            float _SpeckSize;
            float _MinScale;
            float _MaxScale;
            float _FadeIn;
            float _FadeOut;

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

                // Life fade: ramp in over _FadeIn, out over _FadeOut (as fractions of lifetime). Drives
                // both alpha and scale so specks pop in and shrink away rather than blinking.
                float lifeT = s.lifetime > 0.0 ? saturate(s.age / s.lifetime) : 1.0;
                float fadeIn = _FadeIn > 0.0 ? saturate(lifeT / _FadeIn) : 1.0;
                float fadeOut = _FadeOut > 0.0 ? saturate((1.0 - lifeT) / _FadeOut) : 1.0;
                float fade = fadeIn * fadeOut;

                float baseScale = lerp(_MinScale, _MaxScale, hash11(s.seed * 78.233));
                float size = _SpeckSize * baseScale * fade;

                // 2D game on the XY plane — offset in world XY, no billboard basis needed.
                float3 world = float3(s.position + corner * size, 0.0);

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
