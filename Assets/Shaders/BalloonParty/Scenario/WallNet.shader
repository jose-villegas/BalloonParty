Shader "BalloonParty/Scenario/WallNet"
{
    // The play-area frame drawn as four procedural net strips (WallNetView builds the meshes). The camera
    // looks top-down, so a net standing along each wall reads edge-on: at REST every across-row is pulled
    // back onto the wall's edge line, collapsing the band to a thin taut line. The shared disturbance
    // field is what reveals it — where anything stamps the field near an edge (a projectile bounce, a pop),
    // the band UNFURLS inward to its full depth and BULGES along the impact direction, exposing the tennis
    // -net weave and its depth right at the contact point. Pure vertex work, no CPU sim (BushLeaf tap).
    // The dreamy glow leans on HDR + bloom, not saturation.
    Properties
    {
        [HDR] _Color ("Net Color (HDR)", Color) = (1.4, 1.55, 1.7, 0.9)
        _LineWidth ("Line Half-Width (uv)", Range(0.01, 0.5)) = 0.08
        _LineSoftness ("Line Softness (uv)", Range(0.0, 0.5)) = 0.06
        _EdgeFeather ("Band Edge Feather (across)", Range(0.0, 0.5)) = 0.12
        _DepthShade ("Inner-Edge Depth Shade", Range(0.0, 1.0)) = 0.55

        [Header(Reveal (driven by the disturbance field))]
        _OpenGain ("Open Gain", Float) = 4.0
        _RestOpen ("Rest Open (thin-line sliver)", Range(0.0, 0.3)) = 0.04
        _BillowAmplitude ("Billow Amplitude (wu)", Float) = 0.35
        // Driven from WallNetView so the shader's rest un-extrude matches the built geometry width.
        _StripWidth ("Strip Width (driven by C#)", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv0 : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 across : TEXCOORD0;
                float2 net : TEXCOORD1;
                float open : TEXCOORD2;
            };

            // Republished as globals by the disturbance field each blit — no per-material wiring needed.
            sampler2D _DisturbanceTex;
            float2 _FieldBoundsMin;
            float2 _FieldBoundsSize;

            fixed4 _Color;
            float _LineWidth;
            float _LineSoftness;
            float _EdgeFeather;
            float _DepthShade;
            float _OpenGain;
            float _RestOpen;
            float _BillowAmplitude;
            float _StripWidth;

            v2f vert(appdata v)
            {
                v2f o;

                // Full-depth rest position (the mesh is built at full width, extruded OUTWARD from the wall)
                // and the edge's outward normal (away from the play area).
                float3 worldRest = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 outward = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float across = v.uv0.x; // 0 at the wall edge, 1 at the outer lip

                // Sample the disturbance at this row's full-depth rest point so depth reveals with variation.
                float2 fieldUV = (worldRest.xy - _FieldBoundsMin) / _FieldBoundsSize;
                float2 disp = (tex2Dlod(_DisturbanceTex, float4(fieldUV, 0, 0)).gb - 0.5) * 2.0;
                float mag = length(disp);

                // Unfurl: 0 -> flush to the wall edge line (thin line), 1 -> full depth outward. A sliver
                // stays at rest.
                float open = max(_RestOpen, saturate(mag * _OpenGain));

                // Un-extrude back to the wall edge line by how far this row sits outward, relaxing as it opens.
                float3 pos = worldRest - outward * (across * _StripWidth) * (1.0 - open);

                // Billow: the field's displacement is the wake (opposite the motion), so negate it to bulge
                // the sheet WITH the motion — a shot heading outward pushes the net further outward, curving
                // it so the top-down view reads its depth.
                float push = -dot(disp, outward.xy);
                pos += outward * push * _BillowAmplitude;

                o.pos = mul(UNITY_MATRIX_VP, float4(pos, 1.0));
                o.across = v.uv0;
                o.net = v.uv1;
                o.open = open;
                return o;
            }

            // 1 on the thin band around each integer boundary of `coord`, 0 in the cell interior.
            float GridLine(float coord)
            {
                float dist = abs(frac(coord) - 0.5);
                dist = 0.5 - dist; // distance to the nearest integer boundary
                float soft = max(_LineSoftness, 1e-4); // a zero-width smoothstep is undefined on some GPUs
                return 1.0 - smoothstep(_LineWidth, _LineWidth + soft, dist);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float net = max(GridLine(i.net.x), GridLine(i.net.y));

                // Soft band edges across the width so the strip fades in rather than cutting a hard line.
                float u = i.across.x;
                float feather_w = max(_EdgeFeather, 1e-4);
                float feather = smoothstep(0.0, feather_w, u) * smoothstep(0.0, feather_w, 1.0 - u);

                fixed4 col = _Color;
                // Darken toward the inner lip for a curled-depth read as the band opens.
                col.rgb *= lerp(1.0, _DepthShade, u * i.open);
                col.a *= net * feather;
                return col;
            }
            ENDCG
        }
    }
}
