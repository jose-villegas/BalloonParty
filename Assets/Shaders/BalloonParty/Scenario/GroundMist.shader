Shader "BalloonParty/Scenario/GroundMist"
{
    // The mist of the sky/ground reveal (GroundView's mist quad, drawn only while a reveal runs, over the cloud
    // backdrop and under the smoke, the paint and the board). Thick outside the hole the ground shows through
    // (Ground.cginc), it hides the sky there and thins inward across the hole's edge, frayed by two drifting taps of
    // the shared cloud noise so it opens like parting cloud. Lit by the scene light, so it dims through the night.
    Properties
    {
        [NoScaleOffset] _NoiseTex ("Wisp Noise (R, linear)", 2D) = "gray" {}

        [Header(Look)]
        _MistColor      ("Mist Colour",                  Color)           = (0.93, 0.95, 0.98, 1)
        _ShadeColor     ("Thin Mist Colour",             Color)           = (0.74, 0.79, 0.86, 1)
        // World size of the wisps; the second tap is about half as big and drifts the other way.
        _WispSize       ("Wisp Size",                    Float)           = 4.5
        // How far the noise frays the edge band, as a share of its width. Capped at 1: GroundView sizes the fully
        // open hole for at most that much fray, so the mist is gone when the reveal ends.
        _Fray           ("Edge Fray",                    Range(0, 1))     = 0.9
        // Lowest opacity in the mist's thinnest wisps outside the hole: below 1 lets the sky glimmer through.
        _MinThickness   ("Thinnest Mist",                Range(0, 1))     = 0.82

        [Header(Lighting)]
        _LightInfluence ("Light Influence",              Range(0, 1))     = 1

        // Set per renderer by GroundView: how far inside the hole's edge the mist reaches, in world units, and how
        // opaque it is away from the edge.
        [HideInInspector] _MistReach ("Mist Reach", Float) = 1.5
        [HideInInspector] _MistOpacity ("Mist Opacity", Float) = 1
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
        Blend One OneMinusSrcAlpha

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
            half4  _MistColor;
            half4  _ShadeColor;
            float  _WispSize;
            half   _Fray;
            half   _MinThickness;
            half   _LightInfluence;
            float  _MistReach;
            half   _MistOpacity;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                // xy = the large wisps' UV, zw = the small ones', drifting apart with the reveal's unscaled phase.
                float4 wispUV   : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                float2 wp = mul(unity_ObjectToWorld, IN.vertex).xy;
                float2 uv = wp / max(_WispSize, 1e-3);
                float drift = _GroundRevealShape.y * 0.08;
                OUT.wispUV = float4(uv + float2(drift, drift * 0.4), uv * 2.1 + float2(-drift * 1.6, 0.31));
                OUT.worldPos = wp;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                half wisps = tex2D(_NoiseTex, IN.wispUV.xy).r * 0.65h + tex2D(_NoiseTex, IN.wispUV.zw).r * 0.35h;

                // 0 deep inside the hole, 1 at its edge and beyond, the falloff frayed by the wisps so the mist
                // pulls back in tatters, not as a ring.
                float reach = max(_MistReach, 1e-3);
                float distance = GroundRevealDistance(IN.worldPos);
                float inward = distance / reach + 1.0 + (wisps - 0.5h) * _Fray;
                // Never thinner than the sky showing through, so the ground's soft edge never reveals a seam.
                half edge = (half)max(smoothstep(0.0, 1.0, inward), 1.0 - GroundCoverageAt(IN.worldPos));
                // See-through away from the hole, but whole at its edge and across the ground's soft band inside
                // it, so the thinner mist still hides where the ground fades into the sky; it thins out to
                // _MistOpacity over one reach outward, like a cloud bank thickest where it parts.
                edge *= (half)lerp(1.0, _MistOpacity, smoothstep(0.0, reach, distance));

                half thickness = lerp(_MinThickness, 1.0h, saturate(wisps * 1.6h - 0.2h));
                half alpha = (half)_GroundReveal.w * edge * thickness;

                half3 color = lerp(_ShadeColor.rgb, _MistColor.rgb, wisps);
                half3 light = lerp(half3(1.0h, 1.0h, 1.0h), (half3)SceneLightTintAt(IN.worldPos), _LightInfluence);
                return half4(color * light * alpha, alpha);
            }
            ENDCG
        }
    }
}
