#ifndef BALLOONPARTY_SHINE_SWEEP_INCLUDED
#define BALLOONPARTY_SHINE_SWEEP_INCLUDED

// Periodic shine sweep band — shared timing and projection logic.
// Requires the consumer to declare: _ShineWidth, _ShineSpeed, _ShineInterval (or equivalent).

// Pushed every frame by ShaderTimeService. _Time.y is scaled time, so anything driven by it stops
// dead while the game is paused or a popup holds Time.timeScale at 0 — which is exactly when a
// level-up panel wants to keep shining. Zero until the service starts, which reads as "t = 0" for a
// frame rather than as a visual glitch.
float _BP_UnscaledTime;

// The clock a shine should run on. useUnscaled is a [ToggleUI] material property, so this is an
// opt-in: everything that has always run on scaled time keeps doing so untouched.
inline float ShineTime(float useUnscaled)
{
    return useUnscaled > 0.5 ? _BP_UnscaledTime : _Time.y;
}

// Returns the current sweep location along the 0..1 projection axis.
// Range is [-width, 1+width]; the band is visible when within [0,1].
inline float CalcShineSweepLocationAt(float time, float speed, float interval, float width)
{
    float sweepDuration = 1.0 / max(speed, 0.001);
    float cycleDuration = sweepDuration + interval;
    float t = fmod(time, cycleDuration);
    return -width + (1.0 + 2.0 * width) * saturate(t / sweepDuration);
}

inline float CalcShineSweepLocation(float speed, float interval, float width)
{
    return CalcShineSweepLocationAt(_Time.y, speed, interval, width);
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
