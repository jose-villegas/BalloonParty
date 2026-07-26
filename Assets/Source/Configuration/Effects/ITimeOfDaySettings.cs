using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the night-mode time-of-day cycle. The
    /// ambient light direction walks the circle as the player climbs: level 1 sits at the authored
    /// <see cref="ISceneLightSettings.LightDirection"/>, each further level advances by
    /// <see cref="DegreesPerLevel"/>, and the level-up transition sweeps between them.</summary>
    internal interface ITimeOfDaySettings
    {
        /// <summary>Master toggle. Off = the light holds its authored rest direction, no cycle (the
        /// Phase-1 look).</summary>
        bool NightModeEnabled { get; }

        /// <summary>What drives the angle — the level-paced sweep or a continuous wall-clock.</summary>
        TimeOfDaySource Source { get; }

        /// <summary><see cref="TimeOfDaySource.Realtime"/> only: seconds for one full circle (a whole
        /// day). 1800 = 30 min. Advanced on unscaled time, so it runs regardless of pause/time-scale.</summary>
        float SecondsPerCycle { get; }

        /// <summary>Degrees the toward-light direction advances per level, counter-clockwise (the
        /// gradient's <c>t = angle / 360</c>). A full day spans <c>360 / this</c> levels; it wraps
        /// continuously, so the cycle never reverses.</summary>
        float DegreesPerLevel { get; }

        /// <summary>Seconds the level-up sweep takes to walk from the old level's angle to the new one —
        /// unscaled time, so it plays through the transition pause.</summary>
        float SweepDuration { get; }

        /// <summary>Eases the sweep 0→1 over its duration. Linear when unauthored.</summary>
        AnimationCurve SweepEase { get; }

        /// <summary>Start of the night arc, in degrees on the direction circle (default 315). The window
        /// runs from here down to <see cref="NightEndAngle"/>; the light sitting inside it is what
        /// <c>IsNight</c> reports — driving night scoring and the progress-bar night badge.</summary>
        float NightStartAngle { get; }

        /// <summary>End of the night arc, in degrees (default 270). A window authored to wrap past 0
        /// (end &gt; start) is handled.</summary>
        float NightEndAngle { get; }

        /// <summary>Multiplier on the authored GI shadow strength as a function of the light direction
        /// (indexed by <c>Angle01</c>, matched endpoints for the wrap) — deepen shadows toward
        /// dusk/night, lighten at noon. Flat 1 = the base strength unchanged; only applied while night
        /// mode is on.</summary>
        AnimationCurve ShadowStrengthOverAngle { get; }
    }
}
