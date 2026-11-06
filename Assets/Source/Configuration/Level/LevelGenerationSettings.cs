using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     What <see cref="LevelGenerator" /> builds every positive level from: formulas for the level's
    ///     shape and its two budgets, and a catalogue of content pieces that unlock by level. Adding content
    ///     means one catalogue entry, not a new level.
    /// </summary>
    [Serializable]
    internal class LevelGenerationSettings
    {
        [Tooltip("Mixed into every level's seed. Change it to reshuffle every generated level at once.")]
        [SerializeField] private int _seedSalt = 1;

        [Tooltip("Pays for threats: harder balloons and grid actors. Grows with depth, so deeper levels " +
                 "carry more of them.")]
        [SerializeField] private BudgetFormula _threatBudget = new(1f, 0.5f, 20f);

        [Tooltip("Pays for aids: items and helpful balloons. Kept small, so aids rotate between levels " +
                 "instead of piling up.")]
        [SerializeField] private BudgetFormula _aidBudget = new(2f, 0.25f, 4f);

        [Tooltip("Levels a newly unlocked piece is always in, its unlock level included, before it has to " +
                 "win a budget pick like any other.")]
        [SerializeField] [Min(1)] private int _featuredLevels = 2;

        [Tooltip("Palette colours in play, by level. Each step holds until the next.")]
        [SerializeField] private IntLevelStep[] _colors = { new(1, 1) };

        [Tooltip("Lines on the initial board fill, by level. Each step holds until the next.")]
        [SerializeField] private IntLevelStep[] _boardLines = { new(1, 5) };

        [Tooltip("Every this many spawn waves, a wave rolls for items. 0 = never.")]
        [SerializeField] [Min(0)] private int _itemCadence = 1;

        [Tooltip("Item-count distributions, by level. Each step holds until the next.")]
        [SerializeField] private ItemCountStep[] _itemCounts =
        {
            new(1, new AnimationCurve(), new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 1f))),
        };

        [SerializeField] private BalloonContentEntry[] _balloons =
        {
            new(BalloonType.Simple, ContentRole.Threat, unlockLevel: 1, cost: 0, ContentWeightRamp.Flat(1f, 1f)),
        };

        [SerializeField] private ItemContentEntry[] _items = Array.Empty<ItemContentEntry>();

        [SerializeField] private GridActorContentEntry[] _gridActors = Array.Empty<GridActorContentEntry>();

        public int ItemCadence => _itemCadence;
        public int FeaturedLevels => Mathf.Max(1, _featuredLevels);
        public IReadOnlyList<BalloonContentEntry> Balloons => _balloons;
        public IReadOnlyList<ItemContentEntry> Items => _items;
        public IReadOnlyList<GridActorContentEntry> GridActors => _gridActors;

        /// <summary>The most lines any level's initial board fill can hold.</summary>
        public int MaxBoardLines
        {
            get
            {
                var max = 1;
                foreach (var step in _boardLines)
                {
                    max = Mathf.Max(max, step.Value);
                }

                return max;
            }
        }

        public LevelGenerationSettings()
        {
        }

        internal LevelGenerationSettings(BudgetFormula threatBudget, BudgetFormula aidBudget,
            IntLevelStep[] colors, IntLevelStep[] boardLines, ItemCountStep[] itemCounts,
            BalloonContentEntry[] balloons, ItemContentEntry[] items, GridActorContentEntry[] gridActors,
            int itemCadence = 1, int seedSalt = 1, int featuredLevels = 2)
        {
            _threatBudget = threatBudget;
            _aidBudget = aidBudget;
            _featuredLevels = featuredLevels;
            _colors = colors;
            _boardLines = boardLines;
            _itemCounts = itemCounts;
            _balloons = balloons;
            _items = items;
            _gridActors = gridActors;
            _itemCadence = itemCadence;
            _seedSalt = seedSalt;
        }

        public float BudgetForLevel(ContentRole role, int level)
        {
            return role == ContentRole.Aid ? _aidBudget.ForLevel(level) : _threatBudget.ForLevel(level);
        }

        public int ColorsForLevel(int level)
        {
            return Mathf.Clamp(StepValue(_colors, level, 1), 1, 31);
        }

        public int BoardLinesForLevel(int level)
        {
            return Mathf.Max(1, StepValue(_boardLines, level, 1));
        }

        public ItemCountStep ItemCountsForLevel(int level)
        {
            var index = LevelSteps.IndexAt(_itemCounts, level);
            return index >= 0 ? _itemCounts[index] : default;
        }

        /// <summary>The level's seed: a hash of the level and the salt, so a retry replays the same mix.</summary>
        public int SeedForLevel(int level)
        {
            unchecked
            {
                // Murmur3's finaliser: neighbouring levels get unrelated seeds.
                var h = (uint)level * 0x9E3779B1u ^ (uint)_seedSalt;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return (int)(h & 0x7FFFFFFF);
            }
        }

        /// <summary>One message per authoring problem; empty when the catalogue is sound.</summary>
        internal IReadOnlyList<string> Validate(string configName)
        {
            var issues = new List<string>();

            var hasStapleBalloon = false;
            foreach (var entry in _balloons)
            {
                hasStapleBalloon |= entry.UnlockLevel <= 1 && entry.Cost == 0 && entry.Weight.PeakWeight > 0f;
                CheckEntry(entry.Type.ToString(), entry.UnlockLevel, entry.Weight, configName, issues);
            }

            foreach (var entry in _items)
            {
                CheckEntry(entry.Type.ToString(), entry.UnlockLevel, entry.Weight, configName, issues);
            }

            foreach (var entry in _gridActors)
            {
                CheckUnlock(entry.Type.ToString(), entry.UnlockLevel, configName, issues);
            }

            if (!hasStapleBalloon)
            {
                issues.Add($"LevelPacingConfiguration ({configName}): no balloon type is a level-1 staple " +
                           "(unlock 1, cost 0, positive weight), so a level could generate with nothing to spawn.");
            }

            CheckDuplicates(_balloons, e => e.Type.ToString(), configName, issues);
            CheckDuplicates(_items, e => e.Type.ToString(), configName, issues);
            CheckDuplicates(_gridActors, e => e.Type.ToString(), configName, issues);
            return issues;
        }

        private static int StepValue(IntLevelStep[] steps, int level, int fallback)
        {
            var index = LevelSteps.IndexAt(steps, level);
            return index >= 0 ? steps[index].Value : fallback;
        }

        private static void CheckEntry(string name, int unlockLevel, ContentWeightRamp weight, string configName,
            List<string> issues)
        {
            CheckUnlock(name, unlockLevel, configName, issues);
            if (!weight.IsValid())
            {
                issues.Add($"LevelPacingConfiguration ({configName}): catalogue entry {name} needs a positive " +
                           "weight somewhere, and each range's minimum at most its maximum.");
            }
        }

        private static void CheckUnlock(string name, int unlockLevel, string configName, List<string> issues)
        {
            if (unlockLevel < 1)
            {
                issues.Add($"LevelPacingConfiguration ({configName}): catalogue entry {name} unlocks at level " +
                           $"{unlockLevel}; levels start at 1.");
            }
        }

        private static void CheckDuplicates<T>(T[] entries, Func<T, string> key, string configName, List<string> issues)
        {
            var seen = new HashSet<string>();
            foreach (var entry in entries)
            {
                if (!seen.Add(key(entry)))
                {
                    issues.Add($"LevelPacingConfiguration ({configName}): catalogue entry {key(entry)} appears " +
                               "twice; the level would carry it twice.");
                }
            }
        }
    }
}
