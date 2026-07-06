using BalloonParty.Configuration.Level;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The single read surface for the live per-level difficulty mix. Runtime systems inject and
    ///     pull from this — never from <c>ILevelPacingConfiguration</c> or the catalog configs
    ///     directly, so there's exactly one source of truth for "what's active right now."
    ///     <see cref="Current" /> is the resolved level; <see cref="PointsRequiredForLevel" /> stays
    ///     here because it's a cross-level query (points to complete an arbitrary level), not a
    ///     property of the current resolved level — and it must stay independent of resolve order.
    /// </summary>
    internal interface IActiveLevelParameters
    {
        ILevelParameters Current { get; }

        /// <summary>Composed formula × threshold-modifier-curve result for the given level.</summary>
        int PointsRequiredForLevel(int level);
    }
}
