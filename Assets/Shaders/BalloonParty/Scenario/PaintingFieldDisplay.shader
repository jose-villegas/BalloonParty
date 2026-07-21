Shader "BalloonParty/Scenario/PaintingFieldDisplay"
{
    // Full-screen display of the painting field RT as a watercolor wash behind the cloud backdrop.
    // Placed on a quad spanning the viewport, sortingOrder below the clouds. It reads the global
    // _PaintingTex (pushed by PaintingFieldService) and applies edge-bleeding + paper-grain to
    // produce a soft watercolor aesthetic.
    Properties
    {
        _Color          ("Tint",                Color)              = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)

        [Header(Watercolor)]
        _Opacity        ("Opacity",             Range(0, 1))        = 0.8
        _BleedRadius    ("Bleed Radius",        Range(0, 0.02))     = 0.005
        _GrainTex       ("Paper Grain",         2D)                 = "gray" {}
        _GrainStrength  ("Grain Strength",      Range(0, 1))        = 0.15
        _GrainScale     ("Grain Scale",         Float)              = 8.0
        _EdgeSoftness   ("Edge Softness",       Range(0, 1))        = 0.3
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/PaintingField.cginc"

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
                float2 worldPos : TEXCOORD0;
            };

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(fixed4, unity_SpriteRendererColorArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)
                #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
            #else
                fixed4 _RendererColor;
            #endif

            fixed4 _Color;
            float  _Opacity;
            float  _BleedRadius;
            sampler2D _GrainTex;
            float  _GrainStrength;
            float  _GrainScale;
            float  _EdgeSoftness;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.color    = IN.color * _Color * _RendererColor;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            // Watercolor edge-bleed: sample the painting field at offset taps and average.
            float4 SampleBleeded(float2 wp)
            {
                float r = _BleedRadius;

                // 5-tap cross pattern (center + 4 cardinal offsets) for cheap directional bleed.
                float4 center = PaintingFieldSample(wp);
                float4 s1 = PaintingFieldSample(wp + float2( r, 0));
                float4 s2 = PaintingFieldSample(wp + float2(-r, 0));
                float4 s3 = PaintingFieldSample(wp + float2(0,  r));
                float4 s4 = PaintingFieldSample(wp + float2(0, -r));

                // Weight center more heavily so the bleed is subtle.
                float4 avg = center * 0.4 + (s1 + s2 + s3 + s4) * 0.15;
                return avg;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 wp = IN.worldPos;

                float4 paint = SampleBleeded(wp);

                if (paint.a < 0.001)
                {
                    discard;
                }

                // Paper grain: tile a noise texture across world space.
                float2 grainUV = wp * _GrainScale;
                float grain = tex2D(_GrainTex, grainUV).r;
                // Modulate opacity with grain — darkens slightly in textured regions like paper fibers.
                float grainMod = lerp(1.0, grain, _GrainStrength);

                // Edge softness: fade opacity at the edges of paint strokes (low alpha regions).
                float edgeFade = smoothstep(0.0, _EdgeSoftness, paint.a);

                float finalAlpha = paint.a * _Opacity * grainMod * edgeFade * IN.color.a;
                fixed3 finalRgb = paint.rgb * IN.color.rgb * grainMod;

                return fixed4(finalRgb, finalAlpha);
            }
            ENDCG
        }
    }
}
