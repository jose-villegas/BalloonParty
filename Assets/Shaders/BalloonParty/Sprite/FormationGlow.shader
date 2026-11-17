Shader "BalloonParty/Sprite/FormationGlow"
{
    // The blue light ring round unbreakables in formation (FormationGlowView), on the ground beneath them. Each
    // member adds a smooth bump to one field, (1 - d²/reach²)³, zero past its reach, and the ring is drawn where
    // the field crosses a threshold: alone, that's a circle at the ring radius; as members draw close, their
    // fields sum and the rings bulge toward each other, neck and join into one outline round the group, as
    // metaballs do. The distance to that outline is the field's excess over its gradient, so the line keeps one
    // width however the members bunch. Each member's weight (how close its nearest fellow is, and how far into
    // the formation it is) dims its part of the light, blended by the members' share of the field at each point.
    // Additive and HDR: past the bloom threshold, the blue glows.
    Properties
    {
        _Color ("Color", Color) = (0.2, 0.5, 1, 1)
        _HdrBoost ("HDR Boost", Float) = 2.5
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
        Blend One One
        ColorMask RGB

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // Must match FormationGlowField.MaxPoints.
            #define MAX_POINTS 24

            half4 _Color;
            half _HdrBoost;

            // From FormationGlowView: each member's world position (xy) and weight (z); the live count; and
            // (reach², threshold, ring half-width, fill), the lengths in world units.
            float4 _GlowPoints[MAX_POINTS];
            float _GlowCount;
            float4 _GlowField;

            struct appdata_t
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 world  : TEXCOORD0;
            };

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.world = mul(unity_ObjectToWorld, IN.vertex).xy;
                return OUT;
            }

            half4 frag(v2f IN) : SV_Target
            {
                float reach2 = _GlowField.x;
                float field = 0.0;
                float2 gradient = 0.0;
                float weighted = 0.0;

                for (int i = 0; i < MAX_POINTS; i++)
                {
                    if (i >= (int)_GlowCount)
                    {
                        break;
                    }

                    float4 member = _GlowPoints[i];
                    float2 d = IN.world - member.xy;
                    float r2 = dot(d, d);
                    if (r2 < reach2)
                    {
                        float t = 1.0 - r2 / reach2;
                        float k = t * t * t;
                        field += k;
                        gradient += (-6.0 * t * t / reach2) * d;
                        weighted += k * member.z;
                    }
                }

                if (field <= 0.0)
                {
                    return 0;
                }

                // Signed distance to the outline, in world units: positive inside.
                float inside = (field - _GlowField.y) / max(length(gradient), 1e-4);
                float width = _GlowField.z;
                float ring = exp(-(inside * inside) / (width * width));
                float fill = _GlowField.w * smoothstep(0.0, 2.0 * width, inside);
                float light = (ring + fill) * (weighted / field);
                return half4(_Color.rgb * (_HdrBoost * light), 0);
            }
            ENDCG
        }
    }
}
