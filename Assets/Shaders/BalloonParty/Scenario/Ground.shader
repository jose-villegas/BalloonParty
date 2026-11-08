Shader "BalloonParty/Scenario/Ground"
{
    // The floor under the grass blades on ground levels (GroundView's quad, sorted between the sky scatter and
    // the blades): soft patches of darker and lighter ground read from the shared tileable cloud noise, one tap
    // per pixel. Kept dark and muted on purpose: the balloons are saturated and bright, and the board must
    // stand off the floor. The blades (GrassBlades.shader) carry the detail and the reaction to the ball.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Patch Noise (R, linear)", 2D) = "gray" {}

        [Header(Floor)]
        _SoilColor      ("Dark Patch Colour",            Color)           = (0.05, 0.08, 0.06, 1)
        _GrassColor     ("Light Patch Colour",           Color)           = (0.10, 0.15, 0.10, 1)
        _PatchContrast  ("Patch Contrast",               Range(0, 1))     = 0.5

        [Header(Lighting)]
        // Scene-light colour x intensity multiplies the floor: 0 = authored colours always, 1 = fully lit.
        _LightInfluence ("Light Influence",              Range(0, 1))     = 1

        // Opaque by default (One/Zero: no framebuffer read). Exposed so a sky/ground edge can switch the
        // quad to premultiplied blending while it is partly covered.
        [HideInInspector] _SrcBlend ("Src Blend", Float) = 1
        [HideInInspector] _DstBlend ("Dst Blend", Float) = 0
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
        Blend [_SrcBlend] [_DstBlend]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/SceneLight.cginc"

            sampler2D _NoiseTex;
            half4  _SoilColor;
            half4  _GrassColor;
            // Pushed by GroundView from IGroundSettings, so the floor and the blades share one patch scale.
            float  _GroundPatchSize;
            half   _PatchContrast;
            half   _LightInfluence;

            // Pushed by GroundView: the scenario root's displacement. The floor is what the bushes stand on, so
            // on the Ascent (and the restart rise) it travels with them one-to-one.
            float2 _GroundScroll;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 patchUV  : TEXCOORD0;
                half3  light    : TEXCOORD1;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                float2 wp = mul(unity_ObjectToWorld, IN.vertex).xy;
                OUT.patchUV = (wp - _GroundScroll) / max(_GroundPatchSize, 1e-3);
                OUT.light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTint(), _LightInfluence);
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half patch = tex2D(_NoiseTex, IN.patchUV).r;
                half patchMix = saturate(0.5h + (patch - 0.5h) * (1.0h + 2.0h * _PatchContrast));
                return half4(lerp(_SoilColor.rgb, _GrassColor.rgb, patchMix) * IN.light, 1.0h);
            }
            ENDCG
        }
    }
}
