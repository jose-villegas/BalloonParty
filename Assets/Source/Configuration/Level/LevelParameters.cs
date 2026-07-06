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
    /// <summary>
    ///     A fully-resolved level: the runtime output of <see cref="RangedLevelParameters.Resolve" />,
    ///     cached per level by <c>LevelDifficultyResolver</c> and exposed as its <c>Current</c>. Never
    ///     authored or serialized (ranges and customs both author <see cref="RangedLevelParameters" />);
    ///     it only ever exists as a constructed DTO. The authored mix (weights/curves/gates) is set by
    ///     the constructor; the catalog-bridged views (<see cref="_balloonPickList" />, item picks,
    ///     active items, allowed-color names) are filled by the resolver via <see cref="BindResolved" />
    ///     once it has combined this level's weights with the balloon/item catalogs. Field defaults are
    ///     the parameterless fallback (Simple-only) used when no authored range contains a level.
    /// </summary>
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
            new(GridActorType.Puff, 4),
        };

        private readonly int _allowedColorsMask = ~0;

        private IReadOnlyList<ResolvedBalloonEntry> _balloonPickList = Array.Empty<ResolvedBalloonEntry>();
        private IReadOnlyList<ResolvedItemEntry> _itemPickList = Array.Empty<ResolvedItemEntry>();
        private IReadOnlyList<ItemSettings> _items = Array.Empty<ItemSettings>();
        private IReadOnlyList<string> _allowedColorNames = Array.Empty<string>();

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

        // The authored mix the resolver bridges onto the catalog to build the pick lists — not part of
        // the ILevelParameters read surface consumers use.
        internal BalloonTypeWeight[] BalloonWeights => _balloonWeights;
        internal ItemTypeWeight[] ItemWeights => _itemWeights;

        // Fills the catalog-bridged views once the resolver has combined this level's weights with the
        // balloon/item catalogs. Separate from construction because the bridge needs the catalog, which
        // the pure Resolve() path does not have.
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
        }

        public BalloonPrefabEntry PickBalloonEntry(IReadOnlyDictionary<string, int> activeCounts)
        {
            return _balloonPickList.PickRandom(activeCounts)?.Source;
        }

        public ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts)
        {
            return _itemPickList.PickRandom(activeCounts)?.Source;
        }

        public bool TryGetGridActorCount(GridActorType type, out int count)
        {
            foreach (var gate in _gridActorGates)
            {
                if (gate.Type == type)
                {
                    count = gate.Count;
                    return true;
                }
            }

            count = 0;
            return false;
        }
    }

    // A range's balloon weight bridged onto its catalog entry — the unit the weighted pick draws from.
    internal sealed class ResolvedBalloonEntry : IWeightedEntry
    {
        public ResolvedBalloonEntry(BalloonPrefabEntry source, float weight, int maxCount)
        {
            Source = source;
            Weight = weight;
            MaxCount = maxCount;
        }

        public BalloonPrefabEntry Source { get; }
        public float Weight { get; }
        public int MaxCount { get; }
        public string PoolKey => Source.PoolKey;
    }

    internal sealed class ResolvedItemEntry : IWeightedEntry
    {
        public ResolvedItemEntry(ItemSettings source, float weight, int maxCount)
        {
            Source = source;
            Weight = weight;
            MaxCount = maxCount;
        }

        public ItemSettings Source { get; }
        public float Weight { get; }
        public int MaxCount { get; }
        public string PoolKey => Source.Type.ToString();
    }
}
