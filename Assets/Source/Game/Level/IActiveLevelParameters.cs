using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;

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

        /// <summary>Weighted pick honouring the active range's type gate and per-type
        /// <see cref="BalloonTypeWeight.MaxCountOverride" />. Null if every entry is at its cap.</summary>
        BalloonPrefabEntry PickBalloonEntry(IReadOnlyDictionary<string, int> activeCounts);

        /// <summary>How often (in turns) an item-drop opportunity happens this level — replaces
        /// the old per-item <see cref="ItemSettings.TurnCheckEvery" /> catalog cadence.</summary>
        int ItemCadence { get; }

        /// <summary>Weighted distribution (X = item count, Y = weight) for how many items to seed on
        /// the initial board fill, rolled once. Bypasses the turn cadence.</summary>
        AnimationCurve InitialItemCountWeights { get; }

        /// <summary>Weighted distribution (X = item count, Y = weight) for how many items to grant each
        /// time <see cref="ItemCadence" /> hits — rolled per turn, capped by the eligible new balloons.</summary>
        AnimationCurve ItemCountWeights { get; }

        /// <summary>Catalog item entries whose <see cref="ItemType" /> is active this level (the
        /// item type gate) — for iterating candidates, not for picking (see
        /// <see cref="PickItemEntry" />).</summary>
        IReadOnlyList<ItemSettings> Items { get; }

        /// <summary>Same bridge shape as <see cref="PickBalloonEntry" />, for items.</summary>
        ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts);

        IReadOnlyList<string> AllowedColors { get; }

        /// <summary>Same bit-per-color convention as a <c>PaletteColorMaskAttribute</c> field —
        /// for consumers that intersect against their own prefab-level mask (e.g. balloon spawn
        /// color pick) rather than working with names.</summary>
        int AllowedColorsMask { get; }

        /// <summary>
        ///     The resolved count for a grid-actor type this level, or false if the type is absent
        ///     from the active range's gate (cannot spawn at all — same absent-means-excluded
        ///     semantics as the balloon type gate).
        /// </summary>
        bool TryGetGridActorCount(GridActorType type, out int count);
    }
}
