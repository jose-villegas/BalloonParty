using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor.Archetype;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Level;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Resolves and caches the live per-level difficulty mix, bridging range weights onto the balloon/item catalogs.
    /// </summary>
    internal class LevelDifficultyResolver : IStartable, IDisposable, IRunResettable, IActiveLevelParameters, ILevelThresholds
    {
        private readonly ILevelPacingConfiguration _pacing;
        private readonly IBalloonsConfiguration _balloonsConfig;
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;

        private System.Random _rng = new();
        private LevelParameters _current = new();
        private IDisposable _subscription;

        public LevelDifficultyResolver(
            ILevelPacingConfiguration pacing,
            IBalloonsConfiguration balloonsConfig,
            IItemConfiguration itemConfig,
            IGamePalette palette,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber)
        {
            _pacing = pacing;
            _balloonsConfig = balloonsConfig;
            _itemConfig = itemConfig;
            _palette = palette;
            _levelUpSubscriber = levelUpSubscriber;
        }

        public ILevelParameters Current => _current;

        // Must resolve before GridSpawnerCoordinator respawns at Respawn (120).
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
            return _pacing.ThresholdForLevel(level);
        }

        private void ResolveFor(int level)
        {
            var range = ResolveRange(level);
            // A cleared Ranges list or an unassigned Parameters (both inspector misconfigurations) would
            // otherwise NRE here — fall back to defaults, matching the guarded sibling lookups.
            var parameters = range.Parameters ?? new RangedLevelParameters();
            _current = parameters.Resolve(range.PositionOf(level), _rng);

            var itemPickList = new List<ResolvedItemEntry>();
            var activeItems = new List<ItemSettings>();
            BuildItemPickList(_current, itemPickList, activeItems);

            _current.BindResolved(
                BuildBalloonPickList(_current),
                itemPickList,
                activeItems,
                _palette.ColorNamesForMask(_current.AllowedColorsMask));
        }

        // Falls back to the entry marked with -1 in either level bound.
        private LevelRangeEntry ResolveRange(int level)
        {
            var ranges = _pacing.Ranges;
            var fallback = default(LevelRangeEntry);

            for (var i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].IsFallback)
                {
                    fallback = ranges[i];
                    continue;
                }

                if (ranges[i].Contains(level))
                {
                    return ranges[i];
                }
            }

            return fallback;
        }

        private List<ResolvedBalloonEntry> BuildBalloonPickList(LevelParameters parameters)
        {
            var pickList = new List<ResolvedBalloonEntry>();

            foreach (var catalogEntry in _balloonsConfig.Entries)
            {
                if (!TryFindActiveWeight(parameters.BalloonWeights, catalogEntry.BalloonType, out var rangeWeight))
                {
                    // Absent (or zero-weight) from this level's set — the type gate.
                    continue;
                }

                // Weight and per-type cap come solely from the active range now (0 cap = no limit).
                pickList.Add(new ResolvedBalloonEntry(
                    catalogEntry, rangeWeight.Weight, rangeWeight.MaxCountOverride,
                    rangeWeight.InitialCountWeights, rangeWeight.WaveCountWeights));
            }

            return pickList;
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

        private void BuildItemPickList(
            LevelParameters parameters, List<ResolvedItemEntry> pickList, List<ItemSettings> activeItems)
        {
            foreach (var catalogItem in _itemConfig.Items)
            {
                if (!TryFindActiveItemWeight(parameters.ItemWeights, catalogItem.Type, out var rangeWeight))
                {
                    // Absent (or zero-weight) from this level's set — the type gate.
                    continue;
                }

                // Weight comes solely from the active range; the cap still falls back to the item catalog.
                var maxCount = rangeWeight.MaximumAllowedOverride > 0 ? rangeWeight.MaximumAllowedOverride : catalogItem.MaximumAllowed;
                pickList.Add(new ResolvedItemEntry(catalogItem, rangeWeight.Weight, maxCount));
                activeItems.Add(catalogItem);
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
    }
}
