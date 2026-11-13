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
        int BoardLines { get; }

        /// <summary>Every this many spawn waves, the wave rolls for items. 0 = never.</summary>
        int ItemCadence { get; }
        AnimationCurve InitialItemCountWeights { get; }
        AnimationCurve ItemCountWeights { get; }

        IReadOnlyList<ItemSettings> Items { get; }
        IReadOnlyList<string> AllowedColors { get; }
        int AllowedColorsMask { get; }

        /// <summary>
        ///     The time between spawn lines on this level, as a share of the clock's usual: below 1 on an extra-colour
        ///     level (<see cref="ExtraColorLevelSettings" />), 1 otherwise.
        /// </summary>
        float SpawnIntervalScale { get; }

        /// <param name="waveQuotas">Remaining per-wave allowance by pool key; entries at 0 are excluded, absent keys are unlimited.</param>
        BalloonPrefabEntry PickBalloonEntry(
            IReadOnlyDictionary<string, int> activeCounts, IReadOnlyDictionary<string, int> waveQuotas = null);

        /// <summary>Weighted pick restricted to the level's simple-family entries (plain/silver/gold) — used for extra pop-spawns.</summary>
        BalloonPrefabEntry PickSimpleBalloonEntry(IReadOnlyDictionary<string, int> activeCounts);

        /// <summary>Fills guaranteed balloon entries for the initial board fill, respecting caps.</summary>
        void FillGuaranteedBalloons(List<BalloonPrefabEntry> output, Dictionary<string, int> activeCounts);

        /// <summary>Picks guaranteed items for the initial board, respecting caps. Returns the number placed.</summary>
        int FillGuaranteedItems(List<ItemSettings> output, Dictionary<string, int> activeCounts);

        /// <summary>Lucky Find: each of the level's items rolls <paramref name="chance" /> × its
        /// <see cref="ItemSettings.ExtraItemShare" /> for extra copies, respecting caps. Counts
        /// them into <paramref name="activeCounts" /> whether or not a host is found for each.</summary>
        void FillExtraItems(List<ItemSettings> output, Dictionary<string, int> activeCounts, float chance);

        /// <summary>Rolls each curve-bearing type's spawn allowance for the upcoming wave into <paramref name="quotas" /> (absent key = unlimited).</summary>
        void RollWaveQuotas(Dictionary<string, int> quotas, bool isInitial);
        ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts);
        bool TryGetGridActorGate(GridActorType type, out ResolvedGridActorGate gate);
    }
}
