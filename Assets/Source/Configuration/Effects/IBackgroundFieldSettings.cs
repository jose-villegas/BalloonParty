using UnityEngine;

namespace BalloonParty.Configuration.Effects
{
    /// <summary>Read-only tuning for the shared cloud field (see <c>BackgroundFieldService</c>).</summary>
    internal interface IBackgroundFieldSettings
    {
        /// <summary>Blit material (BalloonParty/Display/BackgroundFieldDensity) — the cloud roll's tuning
        /// surface: noise texture, scale, scroll, thresholds all live on it.</summary>
        Material DensityMaterial { get; }

        /// <summary>Display material (BalloonParty/Scenario/BackgroundCloud) on the backdrop SpriteRenderer.</summary>
        Material CloudDisplayMaterial { get; }

        /// <summary>Density-RT resolution per world unit.</summary>
        float TexelsPerUnit { get; }

        /// <summary>How much the scenario's Ascent/descent scrolls the clouds; sign flips the direction.</summary>
        float TransitionParallax { get; }

        /// <summary>Bake cadence authored as "every N frames at 60 fps", reinterpreted as seconds so cost
        /// doesn't scale with display refresh. 1 = every frame at 60 Hz (legacy), 3–4 = recommended for
        /// mobile (the slow-scrolling density is imperceptible at higher rates).</summary>
        float BakeFrameInterval { get; }
    }
}
