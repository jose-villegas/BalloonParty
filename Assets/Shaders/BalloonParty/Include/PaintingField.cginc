#ifndef BALLOONPARTY_PAINTINGFIELD_INCLUDED
#define BALLOONPARTY_PAINTINGFIELD_INCLUDED

// CONSUMER side of the painting field. PaintingFieldService accumulates blended RGB color stamps
// into a global screen-space RT (_PaintingTex). Layout: RGB = blended color, A = opacity.
// Colors mix naturally when overlapping — no palette lookup needed on the consumer side.

sampler2D _PaintingTex;
float2 _PaintingBoundsMin;
float2 _PaintingBoundsSize;
float _PaintingFieldActive;

// World XY -> RT UV, clamped.
float2 PaintingFieldUV(float2 wp)
{
    float2 size = max(_PaintingBoundsSize, 1e-4);
    return saturate((wp - _PaintingBoundsMin) / size);
}

// Blended color + opacity at world position. Returns float4(color.rgb, opacity).
// When the field is inactive, returns (0,0,0,0).
float4 PaintingFieldSample(float2 wp)
{
    float4 data = tex2D(_PaintingTex, PaintingFieldUV(wp));
    return float4(data.rgb, data.a * _PaintingFieldActive);
}

// Vertex-stage variant (tex2Dlod).
float4 PaintingFieldSampleLOD(float2 wp)
{
    float2 uv = PaintingFieldUV(wp);
    float4 data = tex2Dlod(_PaintingTex, float4(uv, 0.0, 0.0));
    return float4(data.rgb, data.a * _PaintingFieldActive);
}

#endif
