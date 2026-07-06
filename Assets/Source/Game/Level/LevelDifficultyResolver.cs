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
    ///     Resolves and caches the live per-level difficulty mix from <see cref="ILevelPacingConfiguration" />.
    ///     Bridges range weights onto the catalog (<see cref="IBalloonsConfiguration" />): a range gates
    ///     which types are active and their relative weight; prefab/pool/HP/VFX still come from the catalog.
    ///     The resolved mix plus the bridged pick lists live on the produced <see cref="Current" />; this
    ///     type just resolves, bridges, and answers the cross-level <see cref="PointsRequiredForLevel" />.
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
            return Mathf.RoundToInt(BasePointsForLevel(level) * _pacing.ThresholdModifier(level));
        }

        private static int BasePointsForLevel(int level)
        {
            return (int)((Mathf.Exp(2) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI))) + 25f);
        }

        private void ResolveFor(int level)
        {
            _current = FindRange(level)?.Resolve(PositionOf(level), _rng) ?? FallbackParameters(level);

            var itemPickList = new List<ResolvedItemEntry>();
            var activeItems = new List<ItemSettings>();
            BuildItemPickList(_current, itemPickList, activeItems);

            _current.BindResolved(
                BuildBalloonPickList(_current),
                itemPickList,
                activeItems,
                _palette.ColorNamesForMask(_current.AllowedColorsMask));
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

                var maxCount = rangeWeight.MaxCountOverride > 0 ? rangeWeight.MaxCountOverride : catalogEntry.MaxCount;
                pickList.Add(new ResolvedBalloonEntry(catalogEntry, catalogEntry.Weight * rangeWeight.Weight, maxCount));
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

                var maxCount = rangeWeight.MaximumAllowedOverride > 0 ? rangeWeight.MaximumAllowedOverride : catalogItem.MaximumAllowed;
                pickList.Add(new ResolvedItemEntry(catalogItem, catalogItem.Weight * rangeWeight.Weight, maxCount));
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
