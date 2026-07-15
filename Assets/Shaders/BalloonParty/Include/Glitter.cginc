#ifndef BALLOONPARTY_GLITTER_INCLUDED
#define BALLOONPARTY_GLITTER_INCLUDED

#include "MathConst.cginc"

// Cheap deterministic 2D hash → pseudo-random value in [0, 1). No texture lookup needed.
inline float Hash21(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

// Scattered twinkling specks: tile UV into a grid, jitter each speck off its cell centre,
// only some cells sparkle at all, and each blinks at its own random phase/speed.
inline fixed GlitterAmountBase(float2 cellUv, float glitterSize, float glitterSpeed,
                               float glitterSharpness, float glitterChance)
{
    float2 cellId  = floor(cellUv);
    float2 cellPos = frac(cellUv) - 0.5;

    float2 jitter = float2(Hash21(cellId + 17.0), Hash21(cellId + 91.0)) - 0.5;
    float  dist   = length(cellPos - jitter * 0.6);
    float  speck  = smoothstep(glitterSize, 0.0, dist);

    float rnd     = Hash21(cellId);
    float phase   = rnd * BP_TAU;
    float twinkle = saturate(sin(_Time.y * glitterSpeed + phase) * 0.5 + 0.5);
    twinkle = pow(twinkle, max(glitterSharpness, 1.0));

    float active = step(1.0 - glitterChance, Hash21(cellId + 5.0));

    return speck * twinkle * active;
}

#endif // BALLOONPARTY_GLITTER_INCLUDED
