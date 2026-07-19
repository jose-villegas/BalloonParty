Shader "BalloonParty/Display/CloudFieldDensity"
{
    // Fills the shared cloud-density RT (see CloudFieldService): each texel is the procedural cloud
    // intensity at the world position that texel covers. CloudFieldService blits this full-screen once
    // per frame; consumers then tap the RT via CloudField.cginc's CloudFieldDensity() instead of
    // recomputing the noise. This material IS the cloud roll's tuning surface — the noise params are
    // material properties here; world bounds arrive from the _CloudFieldBounds* globals the service pushes.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Tileable Noise (R, linear)", 2D) = "gray" {}
        _NoisePeriod        ("Baked Noise Period",  Float)             = 8.0
        _NoiseScale         ("Global Noise Scale",  Float)             = 1.0
        _BaseScale          ("Base Octave Scale",   Float)             = 2.0
        _DetailScale        ("Detail Octave Scale", Float)             = 5.0
        _FineScale          ("Fine Octave Scale",   Float)             = 10.0
        _ScrollSpeedBase    ("Scroll Speed Base",   Vector)            = (0.03, 0.02, 0, 0)
        _ScrollSpeedDetail  ("Scroll Speed Detail", Vector)            = (0.06, -0.04, 0, 0)
        _ScrollSpeedFine    ("Scroll Speed Fine",   Vector)            = (-0.04, 0.08, 0, 0)
        _EdgeLow            ("Edge Low Threshold",  Range(0, 1))       = 0.35
        _EdgeHigh           ("Edge High Threshold", Range(0, 1))       = 0.55
        _AnimationSpeed     ("Animation Speed",     Float)             = 0.8
        _TimeOffset         ("Time Offset (edit)",  Float)             = 0.0
        // How far the disturbance field shoves the cloud (world units). 0 = thin-only, no displacement.
        _DisplaceWorldScale ("Disturbance Displace Scale", Range(0, 2)) = 0.5
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"
            #include "../Include/CloudFieldGen.cginc"

            // Runtime-derived world bounds, pushed as globals by CloudFieldService (NOT material props, so
            // they aren't masked). Consumers read the same globals from CloudField.cginc.
            float2 _CloudFieldBoundsMin;
            float2 _CloudFieldBoundsSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // The texel's world position = field bounds mapped by the blit UV. R = density (shape),
                // G = smooth intensity.
                float2 worldPos = _CloudFieldBoundsMin + i.uv * _CloudFieldBoundsSize;
                return float4(CloudFieldGenerate(worldPos), 0.0, 0.0);
            }
            ENDCG
        }
    }
}
