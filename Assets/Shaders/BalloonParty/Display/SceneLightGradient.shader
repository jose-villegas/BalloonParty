Shader "Hidden/BalloonParty/SceneLightGradient"
{
    // Pass 3 of the light-field chain (see @ref plan_lighting "Milestone 3"): recomputes the GB
    // direction from the R magnitude field. The gradient of R points toward INCREASING brightness =
    // toward the source, which is exactly the toward-light convention the rest of the system uses;
    // this is what makes area lights (paint brightness only) get plausible directions for free.
    //
    // Where the field is flat (rest, no stamps) the gradient is zero and the pass passes the rest GB
    // through VERBATIM (a lerp weight of exactly 0), so the rest field is bit-identical after this
    // pass. R and A are always passed through untouched.
    Properties
    {
        _MainTex     ("Field (read)",     2D)    = "black" {}
        _GradientLo  ("Gradient onset",   Float) = 0.002
        _GradientHi  ("Gradient full",    Float) = 0.05
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
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4    _FieldTexelSize; // xy = (1/width, 1/height); set from C# (depends on RT size)
            float     _GradientLo;     // |grad R| below this = flat, keep the rest direction
            float     _GradientHi;     // |grad R| at/above this = fully gradient-driven

            struct appdata
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
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
                o.uv     = v.texcoord;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 current = tex2D(_MainTex, uv);
                float2 texel = _FieldTexelSize.xy;

                // Central difference of R. Clamp wrap keeps edge taps equal to the centre at rest,
                // so a flat field yields an exactly-zero gradient (no boundary artefact).
                float rLeft  = tex2D(_MainTex, uv - float2(texel.x, 0.0)).r;
                float rRight = tex2D(_MainTex, uv + float2(texel.x, 0.0)).r;
                float rDown  = tex2D(_MainTex, uv - float2(0.0, texel.y)).r;
                float rUp    = tex2D(_MainTex, uv + float2(0.0, texel.y)).r;

                float2 grad = float2(rRight - rLeft, rUp - rDown);
                float gradLen = length(grad);

                // Rest GB is passed through as-is at weight 0, so lerp(restGB, ..., 0) == restGB
                // bit-for-bit. As the gradient strengthens we blend smoothly toward its (biased)
                // direction — no hard switch, so no seam between the lit region and the rest field.
                float2 restGB = current.gb;
                float2 gradGB = (gradLen > 1e-5 ? grad / gradLen : (restGB * 2.0 - 1.0)) * 0.5 + 0.5;
                float weight = smoothstep(_GradientLo, _GradientHi, gradLen);
                float2 outGB = lerp(restGB, gradGB, weight);

                return float4(current.r, outGB, current.a);
            }
            ENDCG
        }
    }
}
