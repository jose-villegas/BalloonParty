#ifndef BALLOONPARTY_SHINE_SWEEP_INCLUDED
#define BALLOONPARTY_SHINE_SWEEP_INCLUDED

// Periodic shine sweep band — shared timing and projection logic.
// Requires the consumer to declare: _ShineWidth, _ShineSpeed, _ShineInterval (or equivalent).

// Returns the current sweep location along the 0..1 projection axis.
// Range is [-width, 1+width]; the band is visible when within [0,1].
inline float CalcShineSweepLocation(float speed, float interval, float width)
{
    float sweepDuration = 1.0 / max(speed, 0.001);
    float cycleDuration = sweepDuration + interval;
    float t = fmod(_Time.y, cycleDuration);
    return -width + (1.0 + 2.0 * width) * saturate(t / sweepDuration);
}

// Projects the UV onto the sweep axis.
// useSceneLight: axis derives from lightDir (down-light); otherwise classic 45° diagonal.
inline float CalcShineProjection(float2 uv, float2 lightDir, float useSceneLight)
{
    return useSceneLight > 0.5
        ? dot(uv - 0.5, -lightDir) + 0.5
        : (uv.x + uv.y) / 2;
}

// Returns the additive shine intensity (0..1) given projection, location, and width.
// Zero when the projection falls outside the band.
inline fixed CalcShineFade(float projection, float location, float width)
{
    float inside = step(location - width, projection) * step(projection, location + width);
    return inside * (1.0 - abs(projection - location) / width);
}

#endif // BALLOONPARTY_SHINE_SWEEP_INCLUDED
