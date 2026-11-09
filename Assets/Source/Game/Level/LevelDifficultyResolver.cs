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
    ///     Resolves and caches the live per-level difficulty mix (generated, or a debug preset), bridging its
    ///     weights onto the balloon/item catalogs.
    /// </summary>
    internal class LevelDifficultyResolver : IStartable, IDisposable, IRunResettable, IActiveLevelParameters, ILevelThresholds
    {
        // Keeps the resolve stream apart from the generator's, which is seeded by the same level seed.
        private const int ResolveSeedSalt = 0x5BD1E995;

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
        private readonly IRetryState _retryState;
        private readonly IRunSeed _runSeed;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;

        private LevelParameters _current = new();
        private IDisposable _subscription;

        public LevelDifficultyResolver(
            ILevelPacingConfiguration pacing,
            IBalloonsConfiguration balloonsConfig,
            IItemConfiguration itemConfig,
            IGamePalette palette,
            IRetryState retryState,
            IRunSeed runSeed,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber)
        {
            _pacing = pacing;
            _balloonsConfig = balloonsConfig;
            _itemConfig = itemConfig;
            _palette = palette;
            _retryState = retryState;
            _runSeed = runSeed;
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
            ResolveFor(StartLevel());
        }

        // Dev "play from level N" override (CheatState.StartLevel); 1 in release. Negative values select a
        // debug preset by its FromLevel id (e.g. -100). A pending retry overrides both.
        private int StartLevel()
        {
            if (_retryState.RetryLevel > 0)
            {
                return _retryState.RetryLevel;
            }

#if UNITY_ENABLE_CHECKS || CHEATS_IN_RELEASE
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
            // Seeded by the level, so a retry rolls the same counts as the first attempt did.
            var rng = new System.Random(_pacing.SeedForLevel(level) ^ ResolveSeedSalt);
            _current = _pacing.ParametersForLevel(level, _runSeed.Value).Resolve(0f, rng);

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
                    rangeWeight.InitialCountWeights, rangeWeight.WaveCountWeights,
                    rangeWeight.GuaranteedInitialCount));
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
                pickList.Add(new ResolvedItemEntry(catalogItem, rangeWeight.Weight, maxCount, rangeWeight.GuaranteedInitialCount));
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
