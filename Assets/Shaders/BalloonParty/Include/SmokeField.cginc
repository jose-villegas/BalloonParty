#ifndef BALLOONPARTY_SMOKEFIELD_INCLUDED
#define BALLOONPARTY_SMOKEFIELD_INCLUDED

// CONSUMER side of the smoke field. SmokeFieldService accumulates blended RGB color stamps
// into a global screen-space RT (_SmokeTex). Layout: RGB = blended color, A = opacity.
// Colors mix naturally when overlapping — no palette lookup needed on the consumer side.

sampler2D _SmokeTex;
float2 _SmokeBoundsMin;
float2 _SmokeBoundsSize;
float _SmokeFieldActive;
// xy = 1 / the field's size in texels (SmokeFieldService), for reads that weigh the texels themselves.
float4 _SmokeTexelSize;

// World XY -> RT UV, clamped.
float2 SmokeFieldUV(float2 wp)
{
    float2 size = max(_SmokeBoundsSize, 1e-4);
    return saturate((wp - _SmokeBoundsMin) / size);
}

// Blended color + opacity at world position. Returns float4(color.rgb, opacity).
// When the field is inactive, returns (0,0,0,0).
float4 SmokeFieldSample(float2 wp)
{
    float4 data = tex2D(_SmokeTex, SmokeFieldUV(wp));
    return float4(data.rgb, data.a * _SmokeFieldActive);
}

// Vertex-stage variant (tex2Dlod).
float4 SmokeFieldSampleLOD(float2 wp)
{
    float2 uv = SmokeFieldUV(wp);
    float4 data = tex2Dlod(_SmokeTex, float4(uv, 0.0, 0.0));
    return float4(data.rgb, data.a * _SmokeFieldActive);
}

// The ink read of the field, for paper levels: opacity as the usual bilinear tap, but the colour weighted by each
// texel's own opacity, so the empty (black) texels around a stroke don't darken its edge the way plain filtering does.
float4 SmokeFieldInkSample(float2 wp)
{
    float2 texel = _SmokeTexelSize.xy;
    float2 st = SmokeFieldUV(wp) / max(texel, 1e-6) - 0.5;
    float2 f = frac(st);
    float2 uv00 = (floor(st) + 0.5) * texel;
    float4 t00 = tex2D(_SmokeTex, uv00);
    float4 t10 = tex2D(_SmokeTex, uv00 + float2(texel.x, 0.0));
    float4 t01 = tex2D(_SmokeTex, uv00 + float2(0.0, texel.y));
    float4 t11 = tex2D(_SmokeTex, uv00 + texel);

    float4 w = float4((1.0 - f.x) * (1.0 - f.y), f.x * (1.0 - f.y), (1.0 - f.x) * f.y, f.x * f.y);
    float4 wa = w * float4(t00.a, t10.a, t01.a, t11.a);
    float alpha = wa.x + wa.y + wa.z + wa.w;
    float3 rgb = (t00.rgb * wa.x + t10.rgb * wa.y + t01.rgb * wa.z + t11.rgb * wa.w) / max(alpha, 1e-4);
    return float4(rgb, alpha * _SmokeFieldActive);
}

#endif
