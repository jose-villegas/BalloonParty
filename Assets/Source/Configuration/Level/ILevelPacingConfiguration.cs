using System.Collections.Generic;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Level-range difficulty authoring: ordered ranges + the global level-up threshold modifier.
    ///     Read by <c>LevelDifficultyResolver</c> — no other runtime system should reference this
    ///     directly.
    /// </summary>
    internal interface ILevelPacingConfiguration
    {
        IReadOnlyList<LevelRangeEntry> Ranges { get; }

        /// <summary>
        ///     Dimensionless multiplier over the base points-required-for-level formula for the given
        ///     level — default flat 1.0 (pure formula) until keys are authored.
        /// </summary>
        float ThresholdModifier(int level);
    }
}
