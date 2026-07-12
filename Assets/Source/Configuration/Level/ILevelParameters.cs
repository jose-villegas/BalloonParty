using System.Collections.Generic;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>The read surface of a fully-resolved level; cross-level queries stay on the resolver instead.</summary>
    internal interface ILevelParameters
    {
        int SpawnLines { get; }
        int BoardLines { get; }
        int ItemCadence { get; }

        /// <summary>The turn this level begins spawning lines; earlier turns are a grace period.</summary>
        int FirstSpawnTurn { get; }
        AnimationCurve InitialItemCountWeights { get; }
        AnimationCurve ItemCountWeights { get; }
        IReadOnlyList<ItemSettings> Items { get; }
        IReadOnlyList<string> AllowedColors { get; }
        int AllowedColorsMask { get; }

        /// <param name="waveQuotas">Remaining per-wave allowance by pool key; entries at 0 are excluded, absent keys are unlimited.</param>
        BalloonPrefabEntry PickBalloonEntry(
            IReadOnlyDictionary<string, int> activeCounts, IReadOnlyDictionary<string, int> waveQuotas = null);

        /// <summary>Rolls each curve-bearing type's spawn allowance for the upcoming wave into <paramref name="quotas" /> (absent key = unlimited).</summary>
        void RollWaveQuotas(Dictionary<string, int> quotas, bool isInitial);
        ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts);
        bool TryGetGridActorGate(GridActorType type, out ResolvedGridActorGate gate);
    }
}
