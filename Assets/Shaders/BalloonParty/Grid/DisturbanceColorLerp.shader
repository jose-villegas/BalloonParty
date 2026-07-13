Shader "Hidden/BalloonParty/Grid/DisturbanceColorLerp"
{
    // ── Smoothed palette-colour layer ───────────────────────────────────
    // Eases a persistent colour RT toward the colour the disturbance field's A-channel tag decodes to, so a
    // stamp OVERWRITING another (an instant index flip in the field) becomes a smooth colour crossfade for
    // consumers. Output: RGB = eased palette colour, A = eased strength (tag life). Consumers sample this
    // bilinearly — real colours interpolate, so there's no index-decode banding and no overwrite flicker.
    //
    //   _MainTex        = previous colour RT (read)
    //   _DisturbanceTex = the field (global); A is point-sampled so the target index stays crisp
    //   _DisturbancePalette / _DisturbancePaletteCount = decode palette (global)
    // ─────────────────────────────────────────────────────────────────────
    Properties
    {
        _MainTex("Previous Colour", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            Texture2D _DisturbanceTex;
            SamplerState sampler_point_clamp;
            float4 _DisturbancePalette[16];
            int _DisturbancePaletteCount;

            float _DeltaTime;
            float _ColorLerpSpeed;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 prev = tex2D(_MainTex, i.uv);

                // Decode the field tag at this texel (point-sampled — the index must stay crisp).
                float tagValue = _DisturbanceTex.Sample(sampler_point_clamp, i.uv).a * 16.0;
                int index = (int)(ceil(tagValue) - 1.0);

                // Tagged → aim at that palette colour + its life; untagged → hold the colour and fade strength out.
                float3 targetColor = prev.rgb;
                float targetStrength = 0.0;
                if (tagValue > 0.05 && index >= 0 && index < _DisturbancePaletteCount)
                {
                    targetColor = _DisturbancePalette[index].rgb;
                    targetStrength = tagValue - (float)index;
                }

                float t = saturate(_ColorLerpSpeed * _DeltaTime);
                float3 rgb = lerp(prev.rgb, targetColor, t);
                float a = lerp(prev.a, targetStrength, t);
                return float4(rgb, a);
            }
            ENDCG
        }
    }
}
