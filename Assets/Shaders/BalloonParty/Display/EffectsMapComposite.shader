Shader "BalloonParty/Display/EffectsMapComposite"
{
    // Lays an effects map back over the frame (EffectsMapCompositor, PLAN-EffectsLayerClearing), faded where
    // the clearing mask is open. The map holds the effects premultiplied, its alpha their coverage, so with
    // the mask empty this is the same blend the effects would have made drawing straight into the frame.
    Properties
    {
        [NoScaleOffset] _EffectsMapTex ("Effects Map", 2D) = "black" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType"      = "Transparent"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _EffectsMapTex;

            // Global, from EffectsClearingMaskView: the mask, and (edge low, edge high, strength, unused).
            sampler2D _EffectsClearMask;
            float4 _EffectsClearParams;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 effects = tex2D(_EffectsMapTex, IN.texcoord);
                half mask = tex2D(_EffectsClearMask, IN.texcoord).r;
                half fade = 1.0 - _EffectsClearParams.z
                    * smoothstep(_EffectsClearParams.x, _EffectsClearParams.y, mask);
                return effects * fade;
            }
            ENDCG
        }
    }
}
