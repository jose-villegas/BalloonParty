Shader "BalloonParty/Scenario/Ground"
{
    // The floor under the grass blades on ground levels (GroundView's quad, sorted between the sky scatter and
    // the blades): soft lighter and darker patches of grassy ground with bare-dirt patches, read from the shared
    // tileable cloud noise. Muted mid-tones, so the scene light visibly carries the time of day across it while
    // the saturated balloons still stand off it. The blades (GrassBlades.shader) carry the detail and the
    // reaction to the ball, and thin out over the same dirt patches (Ground.cginc).
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Patch Noise (R, linear)", 2D) = "gray" {}

        [Header(Grass Floor)]
        _SoilColor      ("Dark Patch Colour",            Color)           = (0.20, 0.25, 0.16, 1)
        _GrassColor     ("Light Patch Colour",           Color)           = (0.30, 0.36, 0.21, 1)
        _PatchContrast  ("Patch Contrast",               Range(0, 1))     = 0.5

        [Header(Dirt)]
        _DirtColor      ("Dirt Colour",                  Color)           = (0.46, 0.38, 0.26, 1)
        _DirtDarkColor  ("Dirt Shade Colour",            Color)           = (0.36, 0.29, 0.20, 1)

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
            #include "../Include/Ground.cginc"

            sampler2D _NoiseTex;
            half4  _SoilColor;
            half4  _GrassColor;
            half   _PatchContrast;
            half4  _DirtColor;
            half4  _DirtDarkColor;
            half   _LightInfluence;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                // xy = colour-patch UV, zw = dirt-patch UV, both in ground space (they travel with the board).
                float4 patchUV  : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                float2 wp = mul(unity_ObjectToWorld, IN.vertex).xy;
                float2 ground = wp - _GroundScroll;
                OUT.patchUV = float4(GroundPatchUV(ground), GroundDirtUV(ground));
                OUT.worldPos = wp;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half patch = tex2D(_NoiseTex, IN.patchUV.xy).r;
                half patchMix = saturate(0.5h + (patch - 0.5h) * (1.0h + 2.0h * _PatchContrast));
                half3 grass = lerp(_SoilColor.rgb, _GrassColor.rgb, patchMix);

                // A finer tap roughens the patch outline, so a cluster frays into the grass instead of ending on
                // a smooth blob edge.
                half dirtNoise = tex2D(_NoiseTex, IN.patchUV.zw).r;
                dirtNoise += (tex2D(_NoiseTex, IN.patchUV.zw * 3.3h + 0.5h).r - 0.5h) * 0.14h;
                half dirt = (half)GroundDirtMask(dirtNoise);
                half3 soil = lerp(_DirtDarkColor.rgb, _DirtColor.rgb, patchMix);
                half3 color = lerp(grass, soil, dirt);

                // Per pixel and field-aware, like the sprites over it: the time of day, plus the local light of
                // pops and items where the light field carries it.
                half3 light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTintAt(IN.worldPos), _LightInfluence);
                return half4(color * light, 1.0h);
            }
            ENDCG
        }
    }
}
