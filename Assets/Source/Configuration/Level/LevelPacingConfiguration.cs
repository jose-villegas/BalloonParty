using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Shared.Diagnostics;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     How levels pace: every positive level's mix is generated from <see cref="_generation" />; the
    ///     scoring curve sets each level's cumulative score milestone, split across that level's colours and
    ///     rounded to a clean multiple; the spawn clock sets when lines arrive. Negative levels are authored
    ///     debug presets, reached only through the start-level cheat.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Level Pacing", fileName = "LevelPacingConfiguration")]
    internal class LevelPacingConfiguration : ScriptableObject, ILevelPacingConfiguration
    {
        [Tooltip("Debug presets, each keyed by a negative FromLevel (e.g. -100) that the start-level cheat " +
                 "selects. Positive levels are generated, so a positive entry here is ignored.")]
        [SerializeField] private LevelRangeEntry[] _ranges = Array.Empty<LevelRangeEntry>();

        [Tooltip("Cap each level's points-required DOWN to a multiple of this (e.g. 50 or 70) for clean " +
                 "targets — 732 caps to 700, not 750. 0 or 1 = no capping.")]
        [SerializeField] private int _thresholdRounding = 50;

        [Tooltip("Unified scoring curve — authors cumulative milestones at key levels; intermediate levels are " +
                 "interpolated via Fritsch–Carlson monotone cubic; beyond the last point the tail extrapolates.")]
        [SerializeField] private LevelScoringCurve _scoringCurve;

        [Tooltip("When balloon lines arrive: one line per interval, shorter each level.")]
        [SerializeField] private SpawnClockSettings _spawnClock = new();

        [Tooltip("What every positive level is generated from: a budget formula and a catalogue of " +
                 "content pieces that unlock by level.")]
        [SerializeField] private LevelGenerationSettings _generation = new();

        public IReadOnlyList<LevelRangeEntry> DebugPresets => _ranges;
        public ISpawnClockSettings SpawnClock => _spawnClock;
        internal LevelScoringCurve ScoringCurve => _scoringCurve;
        internal LevelGenerationSettings Generation => _generation;

        private void OnValidate()
        {
#if UNITY_EDITOR
            _scoringCurve.Validate(name);

            // The non-monotonic-threshold check needs how far out to scan; that's scoring-curve data,
            // which isn't on ILevelPacingConfiguration, so it's derived here rather than in the shared
            // validator (see LevelPacingValidator's own doc).
            var levelsToCheck = 0;
            if (!_scoringCurve.IsEmpty)
            {
                var controlPoints = _scoringCurve.ControlPoints;
                levelsToCheck = controlPoints[controlPoints.Count - 1].Level + 5;
            }

            foreach (var issue in LevelPacingValidator.Validate(this, levelsToCheck, name))
            {
                Debug.LogWarning(issue);
            }

            foreach (var issue in _generation.Validate(name))
            {
                Debug.LogWarning(issue);
            }
#endif
        }

        public RangedLevelParameters ParametersForLevel(int level)
        {
            if (level > 0)
            {
                return LevelGenerator.Generate(_generation, level, SeedForLevel(level));
            }

            if (TryFindPreset(level, out var preset))
            {
                // An unassigned Parameters is an inspector misconfiguration; play the defaults over an NRE.
                return preset.Parameters ?? new RangedLevelParameters();
            }

            Log.Warn("LevelPacing", $"No debug preset with FromLevel={level}; generating level 1 instead.");
            return LevelGenerator.Generate(_generation, 1, SeedForLevel(1));
        }

        public int SeedForLevel(int level)
        {
            return _generation.SeedForLevel(level);
        }

        public ScenarioKind ScenarioForLevel(int level)
        {
            // Debug presets (level < 1) come out sky: the first ground level is at least 1.
            return _generation.ScenarioForLevel(level);
        }

        public int ThresholdForLevel(int level)
        {
            var cumThis = _scoringCurve.CumulativeMilestone(level);
            var cumPrev = _scoringCurve.CumulativeMilestone(level - 1);
            var increment = cumThis - cumPrev;
            var perColor = Mathf.RoundToInt(increment / ColorsForLevel(level));
            return Mathf.Max(1, RoundThreshold(perColor));
        }

        public int MaxConcurrentBalloons(BalloonType type, int columns)
        {
            var max = 0;
            var generatedBoard = columns * _generation.MaxBoardLines;
            foreach (var entry in _generation.Balloons)
            {
                if (entry.Type == type && entry.Weight.PeakWeight > 0f)
                {
                    max = Mathf.Max(max, entry.MaxCount > 0 ? entry.MaxCount : generatedBoard);
                }
            }

            // The fallback Simple a balloon-less catalogue generates (see LevelGenerator) is uncapped.
            if (type == BalloonType.Simple && max == 0)
            {
                max = generatedBoard;
            }

            foreach (var preset in _ranges)
            {
                var parameters = preset.Parameters;
                if (parameters == null)
                {
                    continue;
                }

                foreach (var weight in parameters.BalloonWeights)
                {
                    if (weight.Type != type || weight.Weight <= 0f)
                    {
                        continue;
                    }

                    var cap = weight.MaxCount > 0
                        ? weight.MaxCount
                        : columns * parameters.BoardLines;
                    max = Mathf.Max(max, cap);
                }
            }

            return max;
        }

        public int ColorsForLevel(int level)
        {
            if (level > 0 || !TryFindPreset(level, out var preset))
            {
                return _generation.ColorsForLevel(Mathf.Max(1, level));
            }

            var bits = (uint)(preset.Parameters?.AllowedColorsMask ?? 0);
            var count = 0;
            while (bits != 0)
            {
                count += (int)(bits & 1);
                bits >>= 1;
            }

            return Mathf.Max(1, count);
        }

        private int RoundThreshold(int rawPoints)
        {
            if (_thresholdRounding <= 1)
            {
                return rawPoints;
            }

            return Mathf.Max(_thresholdRounding, rawPoints / _thresholdRounding * _thresholdRounding);
        }

        private bool TryFindPreset(int level, out LevelRangeEntry preset)
        {
            foreach (var entry in _ranges)
            {
                if (entry.IsPreset && entry.FromLevel == level)
                {
                    preset = entry;
                    return true;
                }
            }

            preset = default;
            return false;
        }
    }
}
