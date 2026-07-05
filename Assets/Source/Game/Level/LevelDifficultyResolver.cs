using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Resolves the live per-level difficulty mix from <see cref="ILevelPacingConfiguration" />'s
    ///     authored ranges and caches it — the only thing that re-resolves on
    ///     <see cref="ScoreLevelUpMessage" />, so runtime systems (<see cref="IActiveLevelParameters" />
    ///     consumers) just pull the cached values with no message-ordering dependency of their own.
    ///     Bridges range weights onto the catalog (<see cref="IBalloonsConfiguration" />): a range only
    ///     says <em>which</em> types are active and how they're weighted <em>relative to each other</em>;
    ///     prefab/pool/HP/VFX still come from the catalog entry, and its own weight still governs which
    ///     variant (skin) of a gated-in type is picked — the two weights multiply.
    /// </summary>
    internal class LevelDifficultyResolver : IStartable, IDisposable, IRunResettable, IActiveLevelParameters
    {
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IItemConfiguration _itemConfig;
        private readonly IGameConfiguration _gameConfig;
        private readonly IGamePalette _palette;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly List<ResolvedBalloonEntry> _pickList = new();
        private readonly List<ResolvedItemEntry> _itemPickList = new();
        private readonly List<ItemSettings> _itemsList = new();

        private System.Random _rng = new();
        private LevelParameters _current = new();
        private IReadOnlyList<string> _allowedColorNames = Array.Empty<string>();
        private IDisposable _subscription;

        public LevelDifficultyResolver(
            ILevelPacingConfiguration pacing,
            IBalloonsConfiguration balloonsConfig,
            IItemConfiguration itemConfig,
            IGameConfiguration gameConfig,
            IGamePalette palette,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber)
        {
            _pacing = pacing;
            _balloonsConfig = balloonsConfig;
            _itemConfig = itemConfig;
            _gameConfig = gameConfig;
            _palette = palette;
            _levelUpSubscriber = levelUpSubscriber;
        }

        public int SpawnLines => _current.SpawnLines;
        public int BoardLines => _current.BoardLines;
        public int ItemCadence => _current.ItemCadence;
        public IReadOnlyList<ItemSettings> Items => _itemsList;
        public IReadOnlyList<string> AllowedColors => _allowedColorNames;

        // Re-resolves before GridSpawnerCoordinator respawns at Respawn (120), so a restart's first
        // spawn already sees level-1 parameters instead of the dead run's.
        public int ResetOrder => RunResetOrder.Derived;

        public void Start()
        {
            ResolveFor(1);
            _subscription = _levelUpSubscriber.Subscribe(msg => ResolveFor(msg.NewLevel));
        }

        public void ResetRun(int generation)
        {
            _rng = new System.Random(generation);
            ResolveFor(1);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public int PointsRequiredForLevel(int level)
        {
            return Mathf.RoundToInt(_gameConfig.PointsRequiredForLevel(level) * _pacing.ThresholdModifier(level));
        }

        public BalloonPrefabEntry PickBalloonEntry(IReadOnlyDictionary<string, int> activeCounts)
        {
            return _pickList.PickRandom(activeCounts)?.Source;
        }

        public ItemSettings PickItemEntry(IReadOnlyDictionary<string, int> activeCounts)
        {
            return _itemPickList.PickRandom(activeCounts)?.Source;
        }

        public bool TryGetGridActorCount(GridActorType type, out int count)
        {
            foreach (var gate in _current.GridActorGates)
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

        private void ResolveFor(int level)
        {
            _current = FindRange(level)?.Resolve(PositionOf(level), _rng) ?? FallbackParameters(level);
            RebuildPickList(_current);
            RebuildItemPickList(_current);
            _allowedColorNames = _palette.ColorNamesForMask(_current.AllowedColorsMask);
        }

        private RangedLevelParameters FindRange(int level)
        {
            foreach (var range in _pacing.Ranges)
            {
                if (range.Contains(level))
                {
                    return range.Parameters;
                }
            }

            return null;
        }

        private float PositionOf(int level)
        {
            foreach (var range in _pacing.Ranges)
            {
                if (range.Contains(level))
                {
                    return range.PositionOf(level);
                }
            }

            return 0f;
        }

        private static LevelParameters FallbackParameters(int level)
        {
            Debug.LogWarning(
                $"LevelDifficultyResolver: no authored range contains level {level} — falling back to " +
                "default LevelParameters. Check LevelPacingConfiguration for gaps.");
            return new LevelParameters();
        }

        private void RebuildPickList(LevelParameters parameters)
        {
            _pickList.Clear();

            foreach (var catalogEntry in _balloonsConfig.Entries)
            {
                if (!TryFindActiveWeight(parameters.BalloonWeights, catalogEntry.BalloonType, out var rangeWeight))
                {
                    // Absent (or zero-weight) from this level's set — the type gate.
                    continue;
                }

                var maxCount = rangeWeight.MaxCountOverride > 0 ? rangeWeight.MaxCountOverride : catalogEntry.MaxCount;
                _pickList.Add(new ResolvedBalloonEntry(catalogEntry, catalogEntry.Weight * rangeWeight.Weight, maxCount));
            }
        }

        private static bool TryFindActiveWeight(BalloonTypeWeight[] weights, BalloonType type, out BalloonTypeWeight found)
        {
            foreach (var weight in weights)
            {
                if (weight.Type == type && weight.Weight > 0f)
                {
                    found = weight;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private void RebuildItemPickList(LevelParameters parameters)
        {
            _itemPickList.Clear();
            _itemsList.Clear();

            foreach (var catalogItem in _itemConfig.Items)
            {
                if (!TryFindActiveItemWeight(parameters.ItemWeights, catalogItem.Type, out var rangeWeight))
                {
                    // Absent (or zero-weight) from this level's set — the type gate.
                    continue;
                }

                var maxCount = rangeWeight.MaximumAllowedOverride > 0 ? rangeWeight.MaximumAllowedOverride : catalogItem.MaximumAllowed;
                _itemPickList.Add(new ResolvedItemEntry(catalogItem, catalogItem.Weight * rangeWeight.Weight, maxCount));
                _itemsList.Add(catalogItem);
            }
        }

        private static bool TryFindActiveItemWeight(ItemTypeWeight[] weights, ItemType type, out ItemTypeWeight found)
        {
            foreach (var weight in weights)
            {
                if (weight.Type == type && weight.Weight > 0f)
                {
                    found = weight;
                    return true;
                }
            }

            found = default;
            return false;
        }

        private sealed class ResolvedBalloonEntry : IWeightedEntry
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

        private sealed class ResolvedItemEntry : IWeightedEntry
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
}
