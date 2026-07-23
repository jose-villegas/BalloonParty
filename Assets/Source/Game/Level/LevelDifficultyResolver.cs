using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Game.Run;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
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
        // The rainbow balloon's band colours are the level's allowed set — identical for every rainbow on
        // the board — so they ride GLOBAL shader properties pushed once per level here (this resolver is the
        // single owner of the allowed-colours change) instead of per-renderer. See RainbowBalloon.shader.
        private static readonly int RainbowBandColor0Id = Shader.PropertyToID("_RainbowBandColor0");
        private static readonly int RainbowBandColor1Id = Shader.PropertyToID("_RainbowBandColor1");
        private static readonly int RainbowBandColor2Id = Shader.PropertyToID("_RainbowBandColor2");
        private static readonly int RainbowBandColor3Id = Shader.PropertyToID("_RainbowBandColor3");
        private static readonly int RainbowBandCountId = Shader.PropertyToID("_RainbowBandCount");

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
            ResolveFor(StartLevel());
            _subscription = _levelUpSubscriber.Subscribe(msg => ResolveFor(msg.NewLevel));
        }

        public void ResetRun(int generation)
        {
            _rng = new System.Random(generation);
            ResolveFor(StartLevel());
        }

        // Dev "play from level N" override (CheatState.StartLevel); 1 in release. Negative values select a
        // named fallback entry by its FromLevel id (e.g. -999).
        private static int StartLevel()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD || CHEATS_IN_RELEASE
            var start = BalloonParty.Cheats.CheatState.StartLevel;
            return start < 0 ? start : Mathf.Max(1, start);
#else
            return 1;
#endif
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

            var allowedColorNames = _palette.ColorNamesForMask(_current.AllowedColorsMask);

            _current.BindResolved(
                BuildBalloonPickList(_current),
                itemPickList,
                activeItems,
                allowedColorNames);

            Log.Info("LevelDifficulty", $"Resolved level {level}: " +
                $"{allowedColorNames.Count} colors, {itemPickList.Count} item(s), " +
                $"threshold {_pacing.ThresholdForLevel(level)} pts");

            PushRainbowBandGlobals(allowedColorNames);
        }

        // Pushes the level's allowed colours as global shader properties for the rainbow balloon's bands.
        // Once per level (here), not per balloon — every rainbow shares the same set.
        private void PushRainbowBandGlobals(IReadOnlyList<string> colors)
        {
            Shader.SetGlobalColor(RainbowBandColor0Id, RainbowColorAt(colors, 0));
            Shader.SetGlobalColor(RainbowBandColor1Id, RainbowColorAt(colors, 1));
            Shader.SetGlobalColor(RainbowBandColor2Id, RainbowColorAt(colors, 2));
            Shader.SetGlobalColor(RainbowBandColor3Id, RainbowColorAt(colors, 3));
            Shader.SetGlobalFloat(RainbowBandCountId, Mathf.Max(1, colors.Count));
        }

        // Clamps to the last allowed colour when fewer than 4 are unlocked — the shader's _RainbowBandCount
        // already excludes the unused slots from the cycle, so the clamp is just a safe fill.
        private Color RainbowColorAt(IReadOnlyList<string> colors, int index)
        {
            if (colors == null || colors.Count == 0)
            {
                return Color.white;
            }

            return _palette.GetColor(colors[Mathf.Clamp(index, 0, colors.Count - 1)]);
        }

        // The entry with FromLevel == -1 is the default gameplay fallback. Other negative IDs are
        // debug presets reachable only when a negative level is explicitly requested (cheat override).
        private LevelRangeEntry ResolveRange(int level)
        {
            var ranges = _pacing.Ranges;
            var fallback = default(LevelRangeEntry);

            for (var i = 0; i < ranges.Count; i++)
            {
                if (ranges[i].IsFallback)
                {
                    if (level < 0 && ranges[i].FromLevel == level)
                    {
                        return ranges[i];
                    }

                    if (ranges[i].FromLevel == -1)
                    {
                        fallback = ranges[i];
                    }

                    continue;
                }

                if (ranges[i].Contains(level))
                {
                    return ranges[i];
                }
            }

            if (level < 0)
            {
                Log.Warn("LevelDifficulty", $"No fallback entry with FromLevel={level}; using default (-1) fallback.");
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
                    catalogEntry, rangeWeight.Weight, rangeWeight.MaxCount,
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
