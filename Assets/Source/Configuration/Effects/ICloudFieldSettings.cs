using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the shared cloud field (see <c>CloudFieldService</c>).</summary>
    internal interface ICloudFieldSettings
    {
        /// <summary>Blit material (BalloonParty/Display/CloudFieldDensity) — the cloud roll's tuning
        /// surface: noise texture, scale, scroll, thresholds all live on it.</summary>
        Material DensityMaterial { get; }

        /// <summary>Density-RT resolution per world unit.</summary>
        float TexelsPerUnit { get; }

        /// <summary>How much the scenario's Ascent/descent scrolls the clouds; sign flips the direction.</summary>
        float TransitionParallax { get; }
    }
}
