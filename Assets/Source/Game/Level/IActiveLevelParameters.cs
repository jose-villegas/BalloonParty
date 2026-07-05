using System.Collections.Generic;
using BalloonParty.Configuration;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The single read surface for the live per-level difficulty mix. Runtime systems inject and
    ///     pull from this — never from <see cref="ILevelPacingConfiguration" /> or the catalog configs
    ///     (<see cref="IBalloonsConfiguration" />/<see cref="IItemConfiguration" />) directly, so
    ///     there's exactly one source of truth for "what's active right now."
    /// </summary>
    internal interface IActiveLevelParameters
    {
        int SpawnLines { get; }
        int BoardLines { get; }

        /// <summary>Composed formula × threshold-modifier-curve result for the given level.</summary>
        int PointsRequiredForLevel(int level);

        /// <summary>
        ///     Weighted pick of a catalog <see cref="BalloonPrefabEntry" /> honouring the active
        ///     range's type gate (absent/0-weight types are never returned) and per-type
        ///     <see cref="BalloonTypeWeight.MaxCountOverride" />. Null if every eligible entry is at
        ///     its cap.
        /// </summary>
        BalloonPrefabEntry PickBalloonEntry(IReadOnlyDictionary<string, int> activeCounts);

        IReadOnlyList<ItemSettings> Items { get; }
        IReadOnlyList<string> AllowedColors { get; }
    }
}
