using System.Collections.Generic;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Level-range difficulty authoring; read by <c>LevelDifficultyResolver</c> only.
    /// </summary>
    internal interface ILevelPacingConfiguration
    {
        IReadOnlyList<LevelRangeEntry> Ranges { get; }

        /// <summary>Multiplier over the base points-required-for-level formula; default 1.0 until keys are authored.</summary>
        float ThresholdModifier(int level);
    }
}
