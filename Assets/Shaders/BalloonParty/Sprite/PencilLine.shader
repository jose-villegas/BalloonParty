Shader "BalloonParty/Sprite/PencilLine"
{
    // A TrailRenderer or LineRenderer ribbon drawn as a pencil stroke: the graphite thins out toward
    // ragged edges, paper specks show through it, and the pressure wavers along the stroke. All three
    // come from the heads-ups' pencil noise (Assets/Textures/Text/pencil-noise.png: edge in RGB, specks
    // in A), laid in WORLD space, so a stroke that stays put keeps its grain while it is drawn, and a
    // pen sliding back over its own line draws the same grain again rather than a shimmer.
    //
    // V (texcoord.y) runs 0..1 across the ribbon on both renderers, so abs(V*2-1) is the distance from
    // the centre of the stroke to its edge. The colour is the vertex colour (the renderer's gradient)
    // times _Color, premultiplied, like the other trail shaders.
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _PencilNoiseTex ("Pencil Noise (edge RGB, grain A)", 2D) = "black" {}

        [Header(Edge)]
        // How far into the stroke the noise may eat its edge (0 = a clean band, 1 = to the centre).
        _EdgeRoughness ("Edge Roughness", Range(0, 1)) = 0.45
        // World units one tile of the edge noise covers; smaller = finer tearing.
        _EdgeTile ("Edge Noise Tile", Range(0.25, 32)) = 3
        // The width of the fade at the torn edge, as a share of the half-width.
        _EdgeSoftness ("Edge Softness", Range(0.01, 0.5)) = 0.12

        [Header(Grain)]
        // How much paper shows through the graphite where the noise has a speck.
        _Grain ("Grain", Range(0, 1)) = 0.55
        _GrainTile ("Grain Tile", Range(0.25, 32)) = 2.5

        [Header(Pressure)]
        // How much lighter the stroke goes where the hand eased off, along the line.
        _PressureVariation ("Pressure Variation", Range(0, 1)) = 0.3
        _PressureTile ("Pressure Tile", Range(1, 64)) = 12
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

        Cull     Off
        Lighting Off
        ZWrite   Off
        Blend    One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

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
                float  across   : TEXCOORD0;
                float4 noiseUV  : TEXCOORD1; // grain (xy), edge (zw)
                float2 pressureUV : TEXCOORD2;
            };

            sampler2D _PencilNoiseTex;
            fixed4 _Color;
            half _EdgeRoughness;
            half _EdgeTile;
            half _EdgeSoftness;
            half _Grain;
            half _GrainTile;
            half _PressureVariation;
            half _PressureTile;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.color = IN.color * _Color;
                OUT.across = IN.texcoord.y;
                float2 world = mul(unity_ObjectToWorld, IN.vertex).xy;
                OUT.noiseUV = float4(world / _GrainTile, world / _EdgeTile);
                // The pressure noise is read off a different channel from the edge, so the two don't line up.
                OUT.pressureUV = world / _PressureTile + 0.37;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half fromCentre = abs(IN.across * 2 - 1);

                // Where the edge noise is high the graphite stops short of the ribbon's edge.
                half reach = 1 - _EdgeRoughness * tex2D(_PencilNoiseTex, IN.noiseUV.zw).r;
                half coverage = 1 - smoothstep(reach - _EdgeSoftness, reach, fromCentre);

                half speck = tex2D(_PencilNoiseTex, IN.noiseUV.xy).a;
                half pressure = 1 - _PressureVariation * tex2D(_PencilNoiseTex, IN.pressureUV).g;

                half alpha = IN.color.a * coverage * (1 - _Grain * speck) * pressure;
                return fixed4(IN.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
