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

        /// <summary>Points required to reach <paramref name="level" /> — a base value plus logarithmic growth, scaled by the per-level multiplier curve, snapped to a clean multiple.</summary>
        int ThresholdForLevel(int level);

        /// <summary>
        ///     Upper bound on how many of a balloon type can share the board across every range — the pool
        ///     prewarm size. Uncapped ranges (0 cap) scale to their board size (<paramref name="columns" /> ×
        ///     the range's max board lines). Returns 0 for a type no range gates in.
        /// </summary>
        int MaxConcurrentBalloons(BalloonType type, int columns);
    }
}
