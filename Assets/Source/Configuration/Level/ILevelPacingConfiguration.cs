using System.Collections.Generic;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Level-range difficulty authoring: ordered ranges + exact-level overlays + the global
    ///     level-up threshold modifier. Read by <c>LevelDifficultyResolver</c> — no other runtime
    ///     system should reference this directly.
    /// </summary>
    internal interface ILevelPacingConfiguration
    {
        IReadOnlyList<LevelRangeEntry> Ranges { get; }
        IReadOnlyList<CustomLevelEntry> CustomLevels { get; }

        /// <summary>
        ///     Dimensionless multiplier over the base points-required-for-level formula for the given
        ///     level — default flat 1.0 (pure formula) until keys are authored.
        /// </summary>
        float ThresholdModifier(int level);
    }
}
