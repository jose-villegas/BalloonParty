#ifndef BALLOONPARTY_WATER_SURFACE_INCLUDED
#define BALLOONPARTY_WATER_SURFACE_INCLUDED

// The water surface's slope (Water.shader): a slow swell plus the rings WaterRippleBuffer draws travelling out from
// every impact. The slope bends the view of the bottom, catches the highlight and makes the glints. Needs
// Ground.cginc for GroundPointAt (the swell travels with the board).

#include "Ground.cginc"

// The swell, from the shared tileable noise.
sampler2D _NoiseTex;
float _WaveSize;
float _WaveSpeed;
float _WaveHeight;

// How tall the simulated rings stand per unit of the simulation's height.
float _RippleHeight;

// WaterRippleBuffer's height field (r), over the disturbance field's bounds, and its texel size in UV. Black (flat)
// while no water shows.
sampler2D _WaterRippleTex;
float4    _WaterRippleTexel;
float2    _FieldBoundsMin;
float2    _FieldBoundsSize;

// World units the swell's slope is measured across.
#define WATER_SWELL_STEP 0.125

// Two noise layers crossing each other on different headings. The smaller one moves slower, as shorter waves do
// on open water, so the surface never slides as one sheet.
float WaterSwellHeight(float2 worldPos)
{
    float size = max(_WaveSize, 1e-3);
    float2 p = GroundPointAt(worldPos) / size;
    float t = _Time.y * _WaveSpeed / size;
    float a = tex2Dlod(_NoiseTex, float4(p + float2(0.8, 0.6) * t, 0.0, 0.0)).r;
    float b = tex2Dlod(_NoiseTex, float4(p * 1.9 + float2(0.41, 0.17) + float2(-0.75, 0.66) * (t * 1.9 * 0.72), 0.0, 0.0)).r;
    return ((a - 0.5) + (b - 0.5) * 0.45) * _WaveHeight;
}

float2 WaterSwellSlope(float2 worldPos)
{
    float h = WaterSwellHeight(worldPos);
    float hx = WaterSwellHeight(worldPos + float2(WATER_SWELL_STEP, 0.0));
    float hy = WaterSwellHeight(worldPos + float2(0.0, WATER_SWELL_STEP));
    return float2(hx - h, hy - h) / WATER_SWELL_STEP;
}

// The rings at a world position, in the simulation's own units: xy = height change across one texel each way
// (half the difference of the two neighbours), z = curvature (the four neighbours minus four times the middle:
// positive in a trough, where a ring focuses light on the bottom). Outside the field (the overscan past the
// camera) the water is flat.
float3 WaterRippleAt(float2 worldPos)
{
    float2 uv = (worldPos - _FieldBoundsMin) / max(_FieldBoundsSize, 1e-4);
    float2 texel = _WaterRippleTexel.xy;
    float middle = tex2Dlod(_WaterRippleTex, float4(uv, 0.0, 0.0)).r;
    float right = tex2Dlod(_WaterRippleTex, float4(uv + float2(texel.x, 0.0), 0.0, 0.0)).r;
    float left = tex2Dlod(_WaterRippleTex, float4(uv - float2(texel.x, 0.0), 0.0, 0.0)).r;
    float up = tex2Dlod(_WaterRippleTex, float4(uv + float2(0.0, texel.y), 0.0, 0.0)).r;
    float down = tex2Dlod(_WaterRippleTex, float4(uv - float2(0.0, texel.y), 0.0, 0.0)).r;
    float inField = all(uv == saturate(uv)) ? 1.0 : 0.0;
    return float3(0.5 * (right - left), 0.5 * (up - down), right + left + up + down - 4.0 * middle) * inField;
}

// The surface's slope (height change per world unit): the swell plus the rings (WaterRippleAt).
float2 WaterSlope(float2 worldPos, float3 ripple)
{
    float2 texelWorld = _WaterRippleTexel.xy * max(_FieldBoundsSize, 1e-4);
    return WaterSwellSlope(worldPos) + ripple.xy / texelWorld * _RippleHeight;
}

#endif
