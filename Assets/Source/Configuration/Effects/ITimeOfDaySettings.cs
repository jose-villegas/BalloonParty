using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the night-mode time-of-day cycle (see @ref plan_night_mode). The
    /// ambient light direction walks the circle as the player climbs: level 1 sits at the authored
    /// <see cref="ISceneLightSettings.LightDirection"/>, each further level advances by
    /// <see cref="DegreesPerLevel"/>, and the level-up transition sweeps between them.</summary>
    internal interface ITimeOfDaySettings
    {
        /// <summary>Master toggle. Off = the light holds its authored rest direction, no cycle (the
        /// Phase-1 look).</summary>
        bool NightModeEnabled { get; }

        /// <summary>Degrees the toward-light direction advances per level, counter-clockwise (the
        /// gradient's <c>t = angle / 360</c>). A full day spans <c>360 / this</c> levels; it wraps
        /// continuously, so the cycle never reverses.</summary>
        float DegreesPerLevel { get; }

        /// <summary>Seconds the level-up sweep takes to walk from the old level's angle to the new one —
        /// unscaled time, so it plays through the transition pause.</summary>
        float SweepDuration { get; }

        /// <summary>Eases the sweep 0→1 over its duration. Linear when unauthored.</summary>
        AnimationCurve SweepEase { get; }
    }
}
