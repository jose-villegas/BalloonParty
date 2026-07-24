using UnityEngine;

namespace BalloonParty.Shared.SceneLight
{
    /// <summary>Read surface for the LIVE ambient scene light — the current time-of-day state the
    /// runtime owner (<see cref="TimeOfDayService"/>) drives and publishes as shader globals.
    /// Consumers that need the value CPU-side (the sky tint, the GI magnitude reference) read here
    /// rather than the static <c>ISceneLightSettings</c>, so a runtime sweep reaches them. The
    /// static/edit-time fallback still lives on the settings asset.</summary>
    internal interface ISceneLightRuntime
    {
        /// <summary>The current toward-the-light direction (normalized). Static config today, the
        /// time-of-day sweep target once night mode drives it.</summary>
        Vector2 CurrentDirection { get; }

        /// <summary>The current main-light colour — the solid tint, or the gradient sampled at
        /// <see cref="CurrentDirection"/> when the direction-driven source is on.</summary>
        Color CurrentColor { get; }

        /// <summary>The current light intensity multiplier.</summary>
        float CurrentIntensity { get; }

        /// <summary>Multiplier to apply to the authored GI shadow strength for the current light
        /// direction — the night-mode day/night shadow deepening (1 when night mode is off or the curve
        /// is unauthored, so the base strength is unchanged).</summary>
        float ShadowStrengthScale { get; }
    }
}
