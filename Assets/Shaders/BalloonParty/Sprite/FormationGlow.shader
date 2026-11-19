Shader "BalloonParty/Sprite/FormationGlow"
{
    // The blue light ring round unbreakables in formation (FormationGlowView), on the ground beneath them. Each
    // member adds a smooth bump to its formation's field, (1 - d²/reach²)³, zero past its reach, and the ring is
    // drawn where the field crosses a threshold: alone, that's a circle at the ring radius; as members draw close,
    // their fields sum and the rings bulge toward each other, neck and join into one outline round the group, as
    // metaballs do. Each formation has a field of its own, so two formations side by side keep an outline each
    // and never run into one; where their light overlaps, the brighter shows.
    // The distance to that outline is the field's excess over its gradient, so the line keeps one
    // width however the members bunch. Each member's weight (how close its nearest fellow is, and how far into
    // the formation it is) dims its part of the light, blended by the members' share of the field at each point.
    // A disconnected member's ring dissolves: its presence falls from 1 to 0, its share of the field shrinks to
    // the threshold (so a lone ring draws back into its member and a merge eases off), and the light erodes along
    // a world-space noise as far as the dissolving members' share of the field, with a brighter edge where it
    // eats. Additive and HDR: past the bloom threshold, the blue glows.
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

            // How wide the eaten edge of a dissolving ring is, in noise, and how much brighter it burns.
            #define ERODE_EDGE 0.15
            #define ERODE_GLOW 1.5

            // From FormationGlowView: each member's world position (xy), brightness (z) and presence (w); the
            // live count; (reach², threshold, ring half-width, fill), the lengths in world units; and the
            // dissolve's noise grains per world unit (x).
            float4 _GlowPoints[MAX_POINTS];
            float _GlowCount;
            // Each member's formation. The members of one formation come next to each other.
            float _GlowFormations[MAX_POINTS];
            float4 _GlowField;
            float4 _GlowDissolve;

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

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), u.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), u.x), u.y);
            }

            // One formation's light at a point, from what its members summed there: its field and the field's
            // gradient, their plain sum, and that sum weighted by brightness and by how far each has dissolved.
            // The noise is worked out by the first formation that reaches the point (it is under 0 until then),
            // so a point no ring reaches pays for none.
            float FormationLight(float2 world, float field, float2 gradient, float3 sums, inout float noise)
            {
                float raw = sums.x;
                if (raw <= 0.0)
                {
                    return 0.0;
                }

                // Signed distance to the outline, in world units: positive inside.
                float inside = (field - _GlowField.y) / max(length(gradient), 1e-4);
                float width = _GlowField.z;
                float ring = exp(-(inside * inside) / (width * width));
                float fill = _GlowField.w * smoothstep(0.0, 2.0 * width, inside);

                if (noise < 0.0)
                {
                    float2 grain = world * _GlowDissolve.x;
                    noise = ValueNoise(grain) * 0.65 + ValueNoise(grain * 2.3 + 17.1) * 0.35;
                }

                // Eaten where the noise is under the dissolve's reach: none of it whole, all of it gone.
                float dissolve = sums.z / raw;
                float cut = dissolve * (1.0 + ERODE_EDGE) - ERODE_EDGE;
                float kept = saturate((noise - cut) / ERODE_EDGE);
                float edge = kept * (1.0 - kept) * 4.0 * saturate(dissolve * 8.0);

                return (ring + fill) * (sums.y / raw) * (kept + edge * ERODE_GLOW);
            }

            half4 frag(v2f IN) : SV_Target
            {
                float reach2 = _GlowField.x;
                float threshold = _GlowField.y;
                float field = 0.0;
                float2 gradient = 0.0;
                // x = the members' plain sum, y = weighted by brightness, z = by how far each has dissolved.
                float3 sums = 0.0;
                float light = 0.0;
                float noise = -1.0;
                float formation = _GlowFormations[0];

                for (int i = 0; i < MAX_POINTS; i++)
                {
                    if (i >= (int)_GlowCount)
                    {
                        break;
                    }

                    // The next formation's members start here: the one before is done, and its light is taken.
                    if (_GlowFormations[i] != formation)
                    {
                        light = max(light, FormationLight(IN.world, field, gradient, sums, noise));
                        formation = _GlowFormations[i];
                        field = 0.0;
                        gradient = 0.0;
                        sums = 0.0;
                    }

                    float4 member = _GlowPoints[i];
                    float2 d = IN.world - member.xy;
                    float r2 = dot(d, d);
                    if (r2 < reach2)
                    {
                        float t = 1.0 - r2 / reach2;
                        float k = t * t * t;
                        // Must match FormationGlowField.Shape.
                        float shape = threshold + (1.0 - threshold) * member.w;
                        field += shape * k;
                        gradient += shape * (-6.0 * t * t / reach2) * d;
                        sums += k * float3(1.0, member.z, 1.0 - member.w);
                    }
                }

                light = max(light, FormationLight(IN.world, field, gradient, sums, noise));
                return half4(_Color.rgb * (_HdrBoost * light), 0);
            }
            ENDCG
        }
    }
}
