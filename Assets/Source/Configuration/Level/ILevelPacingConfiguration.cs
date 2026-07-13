using System.Collections.Generic;
using BalloonParty.Balloon.Type;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Level-range difficulty authoring; read by <c>LevelDifficultyResolver</c> (and by the balloon
    ///     spawner's pool prewarm, via <see cref="MaxConcurrentBalloons" />).
    /// </summary>
    internal interface ILevelPacingConfiguration
    {
        IReadOnlyList<LevelRangeEntry> Ranges { get; }

        /// <summary>Multiplier over the base points-required-for-level formula; default 1.0 until keys are authored.</summary>
        float ThresholdModifier(int level);

        /// <summary>Snaps a raw points-required value to a clean multiple (e.g. 50 or 70) so level targets read tidily; identity when rounding is off.</summary>
        int RoundThreshold(int rawPoints);

        /// <summary>
        ///     Upper bound on how many of a balloon type can share the board across every range — the pool
        ///     prewarm size. Uncapped ranges (0 cap) scale to their board size (<paramref name="columns" /> ×
        ///     the range's max board lines). Returns 0 for a type no range gates in.
        /// </summary>
        int MaxConcurrentBalloons(BalloonType type, int columns);
    }
}
