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

            sampler2D _MainTex;
            float4 _ChannelMask;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                float selectedCount = _ChannelMask.r + _ChannelMask.g
                                     + _ChannelMask.b + _ChannelMask.a;

                if (selectedCount <= 1.0)
                {
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
