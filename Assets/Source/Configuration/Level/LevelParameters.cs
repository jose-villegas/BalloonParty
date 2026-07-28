using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Field defaults are the parameterless fallback (Simple-only) used when no authored range contains a level.</summary>
    internal class LevelParameters : ILevelParameters
    {
        private readonly int _spawnLines = 1;
        private readonly int _boardLines = 5;
        private readonly int _itemCadence = 5;
        private readonly int _firstSpawnTurn = 2;
        private readonly AnimationCurve _initialItemCountWeights = new();

        private readonly AnimationCurve _itemCountWeights =
            new(new Keyframe(0f, 0f), new Keyframe(1f, 1f));

        private readonly BalloonTypeWeight[] _balloonWeights =
        {
            new(BalloonType.Simple, 1f),
        };

        private readonly ItemTypeWeight[] _itemWeights = Array.Empty<ItemTypeWeight>();

        private readonly ResolvedGridActorGate[] _gridActorGates =
        {
            new(GridActorType.Puff, 4, 3),
        };

        private readonly int _allowedColorsMask = ~0;

        private IReadOnlyList<ResolvedBalloonEntry> _balloonPickList = Array.Empty<ResolvedBalloonEntry>();
        private IReadOnlyList<ResolvedBalloonEntry> _simpleBalloonPickList = Array.Empty<ResolvedBalloonEntry>();
        private IReadOnlyList<ResolvedItemEntry> _itemPickList = Array.Empty<ResolvedItemEntry>();
        private IReadOnlyList<ItemSettings> _items = Array.Empty<ItemSettings>();
        private IReadOnlyList<string> _allowedColorNames = Array.Empty<string>();

        public int SpawnLines => _spawnLines;
        public int BoardLines => _boardLines;
        public int ItemCadence => _itemCadence;
        public int FirstSpawnTurn => _firstSpawnTurn;
        public AnimationCurve InitialItemCountWeights => _initialItemCountWeights;
        public AnimationCurve ItemCountWeights => _itemCountWeights;
        public IReadOnlyList<ItemSettings> Items => _items;
        public IReadOnlyList<string> AllowedColors => _allowedColorNames;

        /// <summary>Same bit-per-color convention as <see cref="ColorableBalloonVariant" />'s mask — all bits set = every palette color.</summary>
        public int AllowedColorsMask => _allowedColorsMask;

        // Not part of the ILevelParameters read surface consumers use — resolver-only.
        internal BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        internal ItemTypeWeight[] ItemWeights => _itemWeights;

        public LevelParameters()
        {
        }

        internal LevelParameters(
            int spawnLines, int boardLines, int itemCadence, int firstSpawnTurn,
            AnimationCurve initialItemCountWeights, AnimationCurve itemCountWeights,
            BalloonTypeWeight[] balloonWeights, ItemTypeWeight[] itemWeights,
            ResolvedGridActorGate[] gridActorGates, int allowedColorsMask)
        {
            _spawnLines = spawnLines;
            _boardLines = boardLines;
            _itemCadence = itemCadence;
            _firstSpawnTurn = firstSpawnTurn;
            _initialItemCountWeights = initialItemCountWeights;
            _itemCountWeights = itemCountWeights;
            _balloonWeights = balloonWeights;
            _itemWeights = itemWeights;
            _gridActorGates = gridActorGates;
            _allowedColorsMask = allowedColorsMask;
        }

        // Separate from construction because the bridge needs the catalog, unavailable to the pure Resolve() path.
        internal void BindResolved(
            IReadOnlyList<ResolvedBalloonEntry> balloonPickList,
            IReadOnlyList<ResolvedItemEntry> itemPickList,
            IReadOnlyList<ItemSettings> items,
            IReadOnlyList<string> allowedColorNames)
        {
            _balloonPickList = balloonPickList;
            _itemPickList = itemPickList;
            _items = items;
            _allowedColorNames = allowedColorNames;

            // Extra pop-spawns draw only from the simple family this level already gates in, so silver/gold
            // appear here exactly when the range allows them — precomputed once to keep the per-pop pick allocation-free.
            var simple = new List<ResolvedBalloonEntry>();
            foreach (var entry in balloonPickList)
            {
                if (entry.Source.BalloonType.IsSimpleFamily())
                {
                    simple.Add(entry);
                }
            }

            _simpleBalloonPickList = simple;
        }

        public BalloonPrefabEntry PickBalloonEntry(
            IReadOnlyDictionary<string, int> activeCounts, IReadOnlyDictionary<string, int> waveQuotas = null)
        {
            return _balloonPickList.PickRandom(activeCounts, waveQuotas)?.Source;
        }

        public BalloonPrefabEntry PickSimpleBalloonEntry(IReadOnlyDictionary<string, int> activeCounts)
        {
            return _simpleBalloonPickList.PickRandom(activeCounts)?.Source;
        }

        /// <summary>Fills <paramref name="output"/> with guaranteed balloon entries for the initial board,
        /// respecting MaxCount caps. Updates <paramref name="activeCounts"/> as it goes.</summary>
        public void FillGuaranteedBalloons(
            List<BalloonPrefabEntry> output, Dictionary<string, int> activeCounts)
        {
            foreach (var entry in _balloonPickList)
            {
                var guaranteed = entry.GuaranteedInitialCount;
                if (guaranteed <= 0)
                {
                    continue;
                }

                var current = activeCounts.GetValueOrDefault(entry.PoolKey);
                var toAdd = Mathf.Min(guaranteed, entry.MaxCount > 0 ? entry.MaxCount - current : guaranteed);

                for (var i = 0; i < toAdd; i++)
                {
                    output.Add(entry.Source);
                    activeCounts[entry.PoolKey] = activeCounts.GetValueOrDefault(entry.PoolKey) + 1;
                }
            }
        }

        /// <summary>Picks guaranteed items for the initial board, respecting MaxCount caps.
        /// Updates <paramref name="activeCounts"/> as it goes. Returns the number placed.</summary>
        public int FillGuaranteedItems(
            List<ItemSettings> output, Dictionary<string, int> activeCounts)
        {
            var placed = 0;
            foreach (var entry in _itemPickList)
            {
                var guaranteed = entry.GuaranteedInitialCount;
                if (guaranteed <= 0)
                {
                    continue;
                }

                var current = activeCounts.GetValueOrDefault(entry.PoolKey);
                var maxCount = entry.MaxCount;
                var toAdd = maxCount > 0 ? Mathf.Min(guaranteed, maxCount - current) : guaranteed;

                for (var i = 0; i < toAdd; i++)
                {
                    output.Add(entry.Source);
                    activeCounts[entry.PoolKey] = activeCounts.GetValueOrDefault(entry.PoolKey) + 1;
                    placed++;
                }
            }

            return placed;
        }

        // Rolls each curve-bearing type's allowance for the upcoming wave (absent key = unlimited).
        public void RollWaveQuotas(Dictionary<string, int> quotas, bool isInitial)
        {
            quotas.Clear();

            foreach (var entry in _balloonPickList)
            {
                var curve = isInitial ? entry.InitialCountWeights : entry.WaveCountWeights;

                // Wave defaults to initial curve when not provided.
                if (!isInitial && (curve == null || curve.length == 0))
                {
                    curve = entry.InitialCountWeights;
                }

                if (curve != null && curve.length > 0)
                {
                    quotas[entry.PoolKey] = curve.SampleWeightedCount(UnityEngine.Random.value);
                }
            }
        }

        public ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts)
        {
            return _itemPickList.PickRandom(activeCounts)?.Source;
        }

        public bool TryGetGridActorGate(GridActorType type, out ResolvedGridActorGate gate)
        {
            foreach (var candidate in _gridActorGates)
            {
                if (candidate.Type == type)
                {
                    gate = candidate;
                    return true;
                }
            }

            gate = default;
            return false;
        }
    }

    // A range's balloon weight bridged onto its catalog entry.
    internal sealed class ResolvedBalloonEntry : IWeightedEntry
    {
        public BalloonPrefabEntry Source { get; }
        public float Weight { get; }
        public int MaxCount { get; }
        public AnimationCurve InitialCountWeights { get; }
        public AnimationCurve WaveCountWeights { get; }
        public int GuaranteedInitialCount { get; }
        public string PoolKey => Source.PoolKey;

        public ResolvedBalloonEntry(
            BalloonPrefabEntry source,
            float weight,
            int maxCount,
            AnimationCurve initialCountWeights = null,
            AnimationCurve waveCountWeights = null,
            int guaranteedInitialCount = 0)
        {
            Source = source;
            Weight = weight;
            MaxCount = maxCount;
            InitialCountWeights = initialCountWeights;
            WaveCountWeights = waveCountWeights;
            GuaranteedInitialCount = guaranteedInitialCount;
        }
    }

    internal sealed class ResolvedItemEntry : IWeightedEntry
    {
        public ItemSettings Source { get; }
        public float Weight { get; }
        public int MaxCount { get; }
        public int GuaranteedInitialCount { get; }
        public string PoolKey => Source.Type.ToString();

        public ResolvedItemEntry(ItemSettings source, float weight, int maxCount, int guaranteedInitialCount = 0)
        {
            Source = source;
            Weight = weight;
            MaxCount = maxCount;
            GuaranteedInitialCount = guaranteedInitialCount;
        }
    }
}
