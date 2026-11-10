#ifndef BALLOONPARTY_WATER_SURFACE_INCLUDED
#define BALLOONPARTY_WATER_SURFACE_INCLUDED

// The water surface's height (Water.shader): slow noise waves plus the rings the disturbance field's pushes
// raise. Its slope bends the view of the bottom and catches the highlight. Needs Ground.cginc for GroundPointAt
// (the surface travels with the board).

#include "Ground.cginc"

// The surface's own slow waves, from the shared tileable noise.
sampler2D _NoiseTex;
float _WaveSize;
float _WaveSpeed;
float _WaveHeight;

// Rings across a push's intensity (0 at rest, about 1 under a fresh stamp), how fast they walk outward, and how
// tall they stand.
float _RippleRings;
float _RippleSpeed;
float _RippleHeight;

// DisturbanceFieldService's globals: R = signed density (0.5 at rest), GB = displacement (0.5 at rest).
sampler2D _DisturbanceTex;
float2    _FieldBoundsMin;
float2    _FieldBoundsSize;

// The field holds 8 texels per world unit (DisturbanceFieldService): slopes are taken one texel each way.
#define WATER_SLOPE_STEP 0.125

// How hard the field is pushed at a world position: 0 at rest, about 1 under a fresh stamp. Outside the field
// (the overscan past the camera) the water rests.
float WaterPushAt(float2 worldPos)
{
    float2 fieldUV = (worldPos - _FieldBoundsMin) / max(_FieldBoundsSize, 1e-4);
    float3 field = tex2Dlod(_DisturbanceTex, float4(fieldUV, 0.0, 0.0)).rgb;
    float inField = all(fieldUV == saturate(fieldUV)) ? 1.0 : 0.0;
    return saturate(length(field.gb - 0.5) * 2.0 + abs(field.r - 0.5) * 2.0) * inField;
}

// The rings: a wave across the push's intensity, so its crests follow the push's outline, and the phase grows
// with time, so they walk toward the weaker push, outward. They fade with the push.
float WaterRippleHeight(float2 worldPos)
{
    float push = WaterPushAt(worldPos);
    return sin(push * _RippleRings * 6.2832 + _Time.y * _RippleSpeed) * push * _RippleHeight;
}

// Two noise layers drifting across each other: a slow swell with no visible tiling direction.
float WaterWaveHeight(float2 worldPos)
{
    float2 p = GroundPointAt(worldPos) / max(_WaveSize, 1e-3);
    float drift = _Time.y * _WaveSpeed / max(_WaveSize, 1e-3);
    float a = tex2Dlod(_NoiseTex, float4(p + float2(drift, drift * 0.6), 0.0, 0.0)).r;
    float b = tex2Dlod(_NoiseTex, float4(p * 1.37 + float2(0.41, 0.17) - float2(drift * 0.7, -drift), 0.0, 0.0)).r;
    return (a + b - 1.0) * _WaveHeight;
}

float WaterHeight(float2 worldPos)
{
    return WaterWaveHeight(worldPos) + WaterRippleHeight(worldPos);
}

// The surface's slope (height change per world unit) at a world position, from one field texel each way.
float2 WaterSlope(float2 worldPos)
{
    float h = WaterHeight(worldPos);
    float hx = WaterHeight(worldPos + float2(WATER_SLOPE_STEP, 0.0));
    float hy = WaterHeight(worldPos + float2(0.0, WATER_SLOPE_STEP));
    return float2(hx - h, hy - h) / WATER_SLOPE_STEP;
}

#endif
