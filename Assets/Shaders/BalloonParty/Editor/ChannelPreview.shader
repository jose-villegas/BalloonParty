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
            SamplerState sampler_MainTex;      // matches the RT's bilinear filter for R/G/B views
            SamplerState sampler_point_clamp;  // point-samples the palette tag so the index stays crisp
            float4 _ChannelMask;
            float4 _PaletteColors[16];
            float _DecodePalette;  // 1 = solo-channel view decodes the encoded palette index to its color
            float _PaletteChannel; // 0=R, 1=G, 2=B, 3=A — which channel holds the palette index
            float _MipLevel;       // 0 = full res; higher = coarser mip (cone march preview)

            float SampleChannel(float4 tex, int ch)
            {
                if (ch == 0) return tex.r;
                if (ch == 1) return tex.g;
                if (ch == 2) return tex.b;
                return tex.a;
            }

            float ChannelMaskValue(int ch)
            {
                if (ch == 0) return _ChannelMask.r;
                if (ch == 1) return _ChannelMask.g;
                if (ch == 2) return _ChannelMask.b;
                return _ChannelMask.a;
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 tex = _MainTex.SampleLevel(sampler_MainTex, i.uv, _MipLevel);
                float selectedCount = _ChannelMask.r + _ChannelMask.g
                                     + _ChannelMask.b + _ChannelMask.a;

                if (selectedCount <= 1.0)
                {
                    // Solo view of the palette-tagged channel: decode index to its palette color.
                    int palCh = (int)_PaletteChannel;
                    if (_DecodePalette > 0.5 && ChannelMaskValue(palCh) > 0.5)
                    {
                        // Point-sampled: bilinear blend of a quantized index bands into foreign slots.
                        float4 pointSample = _MainTex.Sample(sampler_point_clamp, i.uv);
                        float v = SampleChannel(pointSample, palCh) * 16.0;
                        if (v <= 0.05)
                        {
                            return fixed4(0, 0, 0, 1);
                        }

                        float idx = ceil(v) - 1.0;
                        float life = saturate(v - idx);
                        return fixed4(_PaletteColors[(int)idx].rgb * life, 1);
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
