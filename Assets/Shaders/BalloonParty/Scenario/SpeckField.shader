Shader "BalloonParty/Scenario/SpeckField"
{
    // Renders the GPU speck buffer as camera-facing quads (one per speck), expanded in the vertex shader
    // from SV_VertexID + SV_InstanceID — no mesh, no per-frame CPU. Driven by SpeckField.cs via
    // Graphics.DrawProceduralNow(Triangles, 6, count). STARTER — validate + tune in-editor.
    Properties
    {
        _MainTex   ("Speck Sprite", 2D)          = "white" {}
        _Color     ("Tint",         Color)       = (1, 1, 1, 0.5)
        _SpeckSize ("Speck Size",   Float)       = 0.03
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
            #include "UnityCG.cginc"

            struct Speck
            {
                float2 position;
                float2 velocity;
                float seed;
            };

            StructuredBuffer<Speck> _Specks;
            sampler2D _MainTex;
            fixed4 _Color;
            float _SpeckSize;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            // Two triangles → a unit quad centered on the origin, in [-0.5, 0.5].
            static const float2 Corners[6] =
            {
                float2(-0.5, -0.5), float2(0.5, -0.5), float2(0.5, 0.5),
                float2(-0.5, -0.5), float2(0.5, 0.5), float2(-0.5, 0.5)
            };

            v2f vert(uint vid : SV_VertexID, uint iid : SV_InstanceID)
            {
                Speck s = _Specks[iid];
                float2 corner = Corners[vid];

                // 2D game on the XY plane — offset in world XY, no billboard basis needed.
                float3 world = float3(s.position + corner * _SpeckSize, 0.0);

                v2f o;
                o.pos = UnityWorldToClipPos(world);
                o.uv = corner + 0.5;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.uv) * _Color;
            }
            ENDCG
        }
    }
}
