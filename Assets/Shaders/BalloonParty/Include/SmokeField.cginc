#ifndef BALLOONPARTY_SMOKEFIELD_INCLUDED
#define BALLOONPARTY_SMOKEFIELD_INCLUDED

// CONSUMER side of the smoke field. SmokeFieldService accumulates blended RGB color stamps
// into a global screen-space RT (_SmokeTex). Layout: RGB = blended color, A = opacity.
// Colors mix naturally when overlapping — no palette lookup needed on the consumer side.

sampler2D _SmokeTex;
float2 _SmokeBoundsMin;
float2 _SmokeBoundsSize;
float _SmokeFieldActive;

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

#endif
