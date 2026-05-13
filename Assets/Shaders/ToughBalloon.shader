Shader "Sprites/ToughBalloon"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // -- Damage state (driven from C#) --
        _DamageProgress ("Damage Progress", Range(0,1)) = 0
        _HitTime ("Hit Time", Float) = -999
        _HitFlashDuration ("Hit Flash Duration", Float) = 0.15

        // -- Specular highlight --
        // UV-space offset from sprite center; upper-left by default
        _SpecularOffset ("Specular Offset (UV)", Vector) = (-0.12, 0.12, 0, 0)
        _SpecularColor ("Specular Color", Color) = (0.75, 0.78, 0.85, 1)
        _SpecularRadius ("Specular Radius", Range(0.01, 0.5)) = 0.19
        _SpecularSoftness ("Specular Softness", Range(0.001, 0.3)) = 0.10

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

        // -- Hit flash color --
        _HitFlashColor ("Hit Flash Color", Color) = (0.82, 0.84, 0.90, 1)
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
            };

            sampler2D _MainTex;
            fixed4 _Color;

            float  _DamageProgress;
            float  _HitTime;
            float  _HitFlashDuration;

            float4 _SpecularOffset;
            fixed4 _SpecularColor;
            float  _SpecularRadius;
            float  _SpecularSoftness;

            fixed4 _RimColor;
            float  _RimWidth;

            fixed4 _CrackColor;
            float  _VoronoiScale;
            float  _SphereWarp;
            float2 _VoronoiSeed;
            float  _CrackThreshold;
            float  _CrackSharpness;

            fixed4 _AshColor;
            fixed4 _HitFlashColor;

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
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                fixed4 sprite = tex2D(_MainTex, IN.texcoord) * IN.color;
                float  alpha  = sprite.a;
                if (alpha < 0.01) discard;

                // UV centered at (0,0), range ~[-0.5, 0.5] — rotates with object, used for Voronoi
                float2 uv  = IN.texcoord - 0.5;
                float  r   = length(uv);

                // World-space UV — fixed regardless of object rotation, used for specular & rim.
                // worldExtent = world-space radius of the sprite (X scale of the object).
                float2 worldCenter = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xy;
                float  worldExtent = length(float3(unity_ObjectToWorld._m00,
                                                   unity_ObjectToWorld._m10,
                                                   unity_ObjectToWorld._m20));
                float2 worldUV = (IN.worldPos - worldCenter) / max(worldExtent, 0.0001);

                float  dmg = _DamageProgress;

                // ---- Base: black rubber, goes ashy under stress ----
                fixed3 col = lerp(fixed3(0.04, 0.04, 0.05), _AshColor.rgb, dmg * dmg);

                // ---- Specular highlight (world-space — never rotates) ----------
                float2 specUV   = worldUV - _SpecularOffset.xy;
                specUV.x       *= 0.65;
                float  specDist = length(specUV);

                float  specR    = lerp(_SpecularRadius,        _SpecularRadius * 0.3, dmg);
                float  specSoft = lerp(_SpecularSoftness, _SpecularSoftness * 0.18, dmg);
                float  specAmt  = lerp(0.50,  0.90, dmg);
                float  specular = smoothstep(specR + specSoft, specR - specSoft, specDist) * specAmt;

                col += _SpecularColor.rgb * specular;

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

                float crackLine = smoothstep(_CrackThreshold - 0.02, _CrackThreshold + 0.01, edge);
                crackLine       = pow(crackLine, _CrackSharpness * 0.05);
                float crackFade = smoothstep(0.15, 0.70, dmg);

                float cellFracture = (1.0 - crackLine) * dmg * 0.35;
                col = lerp(col, col * (1.0 - cellFracture), dmg);
                col = lerp(col, _CrackColor.rgb, crackLine * crackFade * (1.0 - rim));

                // ---- Hit flash -----------------------------------------------
                float timeSinceHit = _Time.y - _HitTime;
                if (timeSinceHit >= 0.0 && timeSinceHit < _HitFlashDuration)
                {
                    float t     = 1.0 - (timeSinceHit / _HitFlashDuration);
                    float flash = t * t * 0.75;
                    col = lerp(col, _HitFlashColor.rgb, flash);
                }

                return fixed4(col * alpha, alpha);
            }
            ENDCG
        }
    }
}
