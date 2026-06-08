Shader "Hidden/BalloonParty/Grid/BushBakeBranch"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float edge = abs(i.uv.x - 0.5) * 2.0;
                float aa = 1.0 - smoothstep(0.8, 1.0, edge);
                // RG = direction, B = cross-width position (0–1), A = depth
                return fixed4(i.color.r, i.color.g, i.uv.x, i.color.a * aa);
            }
            ENDCG
        }
    }
}

