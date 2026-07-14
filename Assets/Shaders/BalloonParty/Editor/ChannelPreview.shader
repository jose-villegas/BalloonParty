Shader "Hidden/BalloonParty/Editor/ChannelPreview"
{
    // Editor-only preview blit for GameRenderMapsWindow — masks _MainTex down to the
    // channels the user has toggled on. Exactly one channel selected replicates it as
    // grayscale (so a single R/G/B/A toggle reads clearly on its own); multiple selected
    // keep each channel in its own RGB slot and zero the rest — alpha has no RGB slot of
    // its own, so it only visibly contributes when selected alone. Alpha is always forced
    // to 1 so EditorGUI.DrawPreviewTexture never blends the preview against the window.
    Properties
    {
        _MainTex     ("Texture", 2D)          = "white" {}
        _ChannelMask ("Channel Mask", Vector) = (1, 1, 1, 1)
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            Texture2D _MainTex;
            float4 _MainTex_TexelSize;         // xy = 1/width, 1/height — for the manual palette bilinear
            SamplerState sampler_MainTex;      // matches the RT's bilinear filter for R/G/B views
            SamplerState sampler_point_clamp;  // point-samples the palette tag so each texel's index is exact
            float4 _ChannelMask;
            float4 _PaletteColors[16];
            float _DecodePalette; // 1 = a solo-A view decodes the encoded palette index to its color

            // One texel's A → its palette color, dimmed by the tag's remaining life; black when untagged.
            // A packs (index + life) / 16.
            float3 DecodePaletteTexel(float a)
            {
                float v = a * 16.0;
                if (v <= 0.05)
                {
                    return float3(0, 0, 0);
                }

                float idx = ceil(v) - 1.0;
                float life = saturate(v - idx);
                return _PaletteColors[(int)idx].rgb * life;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 tex = _MainTex.Sample(sampler_MainTex, i.uv);
                float selectedCount = _ChannelMask.r + _ChannelMask.g
                                     + _ChannelMask.b + _ChannelMask.a;

                if (selectedCount <= 1.0)
                {
                    // Solo-A on a palette-tagged map: show the color the encoded index maps to
                    // (black = untagged), instead of near-black grayscale codes.
                    if (_DecodePalette > 0.5 && _ChannelMask.a > 0.5)
                    {
                        // Manual bilinear over the 2x2 texel neighbourhood — decode each texel's index to
                        // a color first, THEN blend, so smooth transitions never band the quantized index
                        // into a foreign slot (what a plain bilinear tap of A would do). Matches how the
                        // game consumers (SceneLight.cginc) sample it.
                        float2 texel = _MainTex_TexelSize.xy;
                        float2 p = i.uv / texel - 0.5;
                        float2 f = frac(p);
                        float2 uv00 = (floor(p) + 0.5) * texel;
                        float3 c00 = DecodePaletteTexel(_MainTex.Sample(sampler_point_clamp, uv00).a);
                        float3 c10 = DecodePaletteTexel(_MainTex.Sample(sampler_point_clamp, uv00 + float2(texel.x, 0.0)).a);
                        float3 c01 = DecodePaletteTexel(_MainTex.Sample(sampler_point_clamp, uv00 + float2(0.0, texel.y)).a);
                        float3 c11 = DecodePaletteTexel(_MainTex.Sample(sampler_point_clamp, uv00 + texel).a);
                        float3 blended = lerp(lerp(c00, c10, f.x), lerp(c01, c11, f.x), f.y);
                        return fixed4(blended, 1);
                    }

                    // Zero or one channel selected — replicate the single value (0 if
                    // none are on) as grayscale, whichever channel it came from.
                    float v = tex.r * _ChannelMask.r + tex.g * _ChannelMask.g
                            + tex.b * _ChannelMask.b + tex.a * _ChannelMask.a;
                    return fixed4(v, v, v, 1);
                }

                // Multiple channels selected — keep each in its own RGB slot, zero the rest.
                return fixed4(tex.rgb * _ChannelMask.rgb, 1);
            }
            ENDCG
        }
    }
}
