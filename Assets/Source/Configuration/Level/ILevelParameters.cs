using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     The read surface of a fully-resolved level: the difficulty mix (spawn/board/cadence and
    ///     item-count curves), the catalog-bridged weighted picks, the active item set, and the
    ///     allowed-color gate. Exposed by the resolver as its <c>Current</c>; the concrete
    ///     <see cref="LevelParameters" /> implements it. Cross-level queries (e.g. points-required)
    ///     stay on the resolver — they're not a property of a single resolved level.
    /// </summary>
    internal interface ILevelParameters
    {
        int SpawnLines { get; }
        int BoardLines { get; }
        int ItemCadence { get; }
        AnimationCurve InitialItemCountWeights { get; }
        AnimationCurve ItemCountWeights { get; }
        IReadOnlyList<ItemSettings> Items { get; }
        IReadOnlyList<string> AllowedColors { get; }
        int AllowedColorsMask { get; }

        BalloonPrefabEntry PickBalloonEntry(IReadOnlyDictionary<string, int> activeCounts);
        ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts);
        bool TryGetGridActorCount(GridActorType type, out int count);
    }
}
