Shader "BalloonParty/Balloon/ToughBalloon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // -- Damage state (driven from C#) --
        _DamageProgress ("Damage Progress", Range(0,1)) = 0

        // -- Rim / subsurface edge --
        _RimColor ("Rim Color", Color) = (0.18, 0.18, 0.22, 1)
        _RimWidth ("Rim Width", Range(0, 0.5)) = 0.11

        // -- Voronoi stress cracks --
        _CrackColor ("Crack Color", Color) = (0.55, 0.55, 0.60, 1)
        _VoronoiScale ("Voronoi Scale", Range(2, 12)) = 4.5
        _SphereWarp ("Sphere Warp Strength", Range(1, 6)) = 2.5
        _VoronoiSeed ("Voronoi Seed", Vector) = (0, 0, 0, 0)
        _CrackThreshold ("Crack Edge Threshold", Range(0.02, 0.15)) = 0.08
        _CrackSharpness ("Crack Sharpness", Range(5, 60)) = 28

        // -- Ash tint at max damage --
        _AshColor ("Ash Color (max damage)", Color) = (0.20, 0.20, 0.22, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex    : SV_POSITION;
                fixed4 color     : COLOR;
                float2 texcoord  : TEXCOORD0;
                float2 worldPos  : TEXCOORD1;
                // xy = world-space object center, z = world-space extent (X scale)
                // Computed in vertex to avoid per-fragment matrix work.
                float3 worldData : TEXCOORD2;
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float  _DamageProgress;

            fixed4 _RimColor;
            float  _RimWidth;

            fixed4 _CrackColor;
            float  _VoronoiScale;
            float  _SphereWarp;
            float2 _VoronoiSeed;
            float  _CrackThreshold;
            float  _CrackSharpness;

            fixed4 _AshColor;

            // ----------------------------------------------------------------
            // Voronoi helpers
            // ----------------------------------------------------------------
            float2 VoronoiHash(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                           dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453123);
            }

            // Returns (nearest distance, second-nearest distance).
            // The difference (y - x) is strongest along cell edges.
            float2 Voronoi(float2 uv)
            {
                float2 cell = floor(uv);
                float2 f    = frac(uv);

                float d0 = 8.0, d1 = 8.0;

                UNITY_UNROLL
                for (int y = -1; y <= 1; y++)
                {
                    UNITY_UNROLL
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 n   = float2(x, y);
                        float2 pt  = VoronoiHash(cell + n);  // random point inside cell
                        float2 dv  = n + pt - f;
                        float  d   = dot(dv, dv);             // squared — avoids sqrt

                        if (d < d0) { d1 = d0; d0 = d; }
                        else if (d < d1) { d1 = d; }
                    }
                }

                // sqrt only for the final values we expose
                return float2(sqrt(d0), sqrt(d1));
            }

            // ----------------------------------------------------------------
            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex   = UnityObjectToClipPos(IN.vertex);
                OUT.texcoord = IN.texcoord;
                OUT.color    = IN.color * _Color;
                OUT.worldPos = mul(unity_ObjectToWorld, IN.vertex).xy;

                float2 worldCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                float  worldExtent = length(float3(unity_ObjectToWorld._m00,
                                                   unity_ObjectToWorld._m10,
                                                   unity_ObjectToWorld._m20));
                OUT.worldData = float3(worldCenter, worldExtent);

#ifdef PIXELSNAP_ON
                OUT.vertex = UnityPixelSnap(OUT.vertex);
#endif
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;
                float  alpha  = sprite.a;
                if (alpha < 0.01) discard;

                float2 uv     = IN.texcoord - 0.5;
                float2 worldUV = (IN.worldPos - IN.worldData.xy) / max(IN.worldData.z, 0.0001);

                float  dmg = _DamageProgress;

                // ---- Base: black rubber, goes ashy under stress ----
                fixed3 col = lerp(fixed3(0.04, 0.04, 0.05), _AshColor.rgb, dmg * dmg);


                // ---- Rim / subsurface fringe (world-space — never rotates) -----
                float rimW  = _RimWidth * (1.0 - dmg * 0.72);
                float rimR  = length(worldUV);
                float rim   = smoothstep(0.50 - rimW, 0.50, rimR) * alpha * (1.0 - dmg * 0.45);
                col         = lerp(col, _RimColor.rgb, rim);

                // ---- Voronoi stress cracks (object UV — cracks live on the surface) ----
                float2 p     = uv * 2.0;
                float  rFlat = min(length(p), 0.9999);
                float  zSph  = sqrt(1.0 - rFlat * rFlat);
                float  phi   = atan2(p.y, p.x);
                float  theta = acos(zSph);

                float  thetaNorm = theta / (UNITY_PI * 0.5);
                float  vorR = pow(thetaNorm, _SphereWarp) * _VoronoiScale;
                float2 vUV  = float2(cos(phi) * vorR, sin(phi) * vorR) + _VoronoiSeed;
                float2 voro = Voronoi(vUV);
                float  edge = voro.y - voro.x;

                // At dmg=0, threshold is 0.25 — only the narrow band at cell boundaries
                // qualifies, giving thin hairlines. As dmg→1 it drops to _CrackThreshold,
                // widening into full splits. Opacity (crackFade) also scales with dmg so
                // early hairlines are nearly transparent and grow opaque alongside their width.
                float dynThreshold = lerp(0.25, _CrackThreshold, dmg);
                float softness     = lerp(0.003, 0.018, dmg);
                float crackLine    = smoothstep(dynThreshold - softness, dynThreshold + softness, edge);
                // No pow crushing at low damage — thin lines need to survive.
                // Sharpness only kicks in as cracks widen.
                crackLine          = pow(crackLine, lerp(1.0, _CrackSharpness * 0.03, dmg));

                float crackFade = dmg;

                float cellFracture = (1.0 - crackLine) * dmg * 0.35;
                col = lerp(col, col * (1.0 - cellFracture), dmg);
                col = lerp(col, _CrackColor.rgb, crackLine * crackFade * (1.0 - rim));

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
}
