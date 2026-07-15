using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Per-colour points-required per level. A covering <see cref="_thresholdOverrides" /> entry sets the
    /// level's cumulative run-score milestone and the per-colour bar is the delta from the previous level split
    /// across that level's colours; uncovered levels use the formula (<see cref="_baseValue" /> + logarithmic
    /// growth).</summary>
    [CreateAssetMenu(menuName = "Configuration/Level Pacing", fileName = "LevelPacingConfiguration")]
    internal class LevelPacingConfiguration : ScriptableObject, ILevelPacingConfiguration
    {
        [SerializeField] private LevelRangeEntry[] _ranges =
        {
            // Construct Parameters explicitly — the struct default zero-initializes it to null.
            new(0, 0, new RangedLevelParameters()),
        };

        [Tooltip("Overrides the formula for the levels each entry spans — the curve's Y is the cumulative " +
                 "run-score milestone for that level, and the per-colour bar is (this milestone − the previous " +
                 "level's) ÷ that level's colours. Any level not covered falls through to the formula below.")]
        [SerializeField] private LevelThresholdOverride[] _thresholdOverrides = Array.Empty<LevelThresholdOverride>();

        [Tooltip("Formula base points — the floor the logarithmic growth builds on. The log term is 0 at level 1, " +
                 "so an un-overridden level 1 equals this value.")]
        [SerializeField] private float _baseValue = 25f;

        [Tooltip("Cap each level's points-required DOWN to a multiple of this (e.g. 50 or 70) for clean " +
                 "targets — 732 caps to 700, not 750. 0 or 1 = no capping.")]
        [SerializeField] private int _thresholdRounding = 50;

        public IReadOnlyList<LevelRangeEntry> Ranges => _ranges;

        private void OnValidate()
        {
#if UNITY_EDITOR
            WarnOnGapsAndOverlaps();
            WarnOnMissingOrMultipleTails();
            WarnOnEmptyWeightedSets();
            WarnOnNonMonotonicThreshold();
#endif
        }

        // An override sets this level's cumulative run-score milestone; the per-colour bar is the increment over
        // the previous level's milestone, split across this level's colours. Uncovered levels use the formula:
        // base + logarithmic growth, then snapped to a clean multiple.
        public int ThresholdForLevel(int level)
        {
            if (TryGetOverride(level, out var entry))
            {
                var increment = entry.CumulativeScore(level) - CumulativeScoreForLevel(level - 1);
                var perColor = Mathf.RoundToInt(increment / ColorsForLevel(level));
                // Floor at 1 either way — a flat/decreasing milestone (or rounding disabled) must never yield a
                // non-positive bar, which would make the win check trivially true and insta-level.
                return Mathf.Max(1, entry.SnapToRounding ? RoundThreshold(perColor) : perColor);
            }

            var scaling = _baseValue + Mathf.Exp(2f) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI));
            return RoundThreshold(Mathf.RoundToInt(scaling));
        }

        // Cap DOWN to the multiple at or below (so 732 → 700, not 750), floored at one multiple so a
        // level never caps to zero.
        private int RoundThreshold(int rawPoints)
        {
            if (_thresholdRounding <= 1)
            {
                return rawPoints;
            }

            return Mathf.Max(_thresholdRounding, rawPoints / _thresholdRounding * _thresholdRounding);
        }

        public int MaxConcurrentBalloons(BalloonType type, int columns)
        {
            var max = 0;
            foreach (var range in _ranges)
            {
                var parameters = range.Parameters;
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

                    // 0 override = uncapped in that range, so it can fill the range's whole board.
                    var cap = weight.MaxCountOverride > 0
                        ? weight.MaxCountOverride
                        : columns * parameters.BoardLines.Max;
                    max = Mathf.Max(max, cap);
                }
            }

            return max;
        }

        // The cumulative milestone an override pins at this level; 0 for levels with no override (the start of a
        // fresh cumulative segment).
        private float CumulativeScoreForLevel(int level)
        {
            return level >= 1 && TryGetOverride(level, out var entry) ? entry.CumulativeScore(level) : 0f;
        }

        private bool TryGetOverride(int level, out LevelThresholdOverride result)
        {
            foreach (var entry in _thresholdOverrides)
            {
                if (entry.Contains(level) && entry.HasCurve)
                {
                    result = entry;
                    return true;
                }
            }

            result = default;
            return false;
        }

        // Popcount of the level's allowed-colour mask — the number of colours the win condition scores that level.
        private int ColorsForLevel(int level)
        {
            var mask = MaskForLevel(level);
            var count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return Mathf.Max(1, count);
        }

        // Mirrors the resolver's lookup: the range containing the level, falling back to the open-ended tail.
        private int MaskForLevel(int level)
        {
            for (var i = 0; i < _ranges.Length; i++)
            {
                if (_ranges[i].Contains(level))
                {
                    return _ranges[i].Parameters?.AllowedColorsMask ?? 0;
                }
            }

            return _ranges.Length > 0 ? _ranges[_ranges.Length - 1].Parameters?.AllowedColorsMask ?? 0 : 0;
        }

#if UNITY_EDITOR
        private void WarnOnGapsAndOverlaps()
        {
            for (var i = 1; i < _ranges.Length; i++)
            {
                var previous = _ranges[i - 1];
                var current = _ranges[i];

                if (previous.IsOpenEnded)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): range starting at {previous.FromLevel} is open-ended " +
                        $"but is followed by a range starting at {current.FromLevel} — the later range is unreachable.");
                    continue;
                }

                if (current.FromLevel != previous.ToLevel + 1)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): gap or overlap between ranges " +
                        $"[{previous.FromLevel}-{previous.ToLevel}] and [{current.FromLevel}-{current.ToLevel}] " +
                        "— ranges must be contiguous.");
                }
            }
        }

        private void WarnOnMissingOrMultipleTails()
        {
            var tailCount = 0;
            for (var i = 0; i < _ranges.Length; i++)
            {
                if (_ranges[i].IsOpenEnded)
                {
                    tailCount++;
                }
            }

            if (tailCount != 1)
            {
                Debug.LogWarning(
                    $"LevelPacingConfiguration ({name}): expected exactly one open-ended tail range, found {tailCount}.");
            }
        }

        private void WarnOnEmptyWeightedSets()
        {
            for (var i = 0; i < _ranges.Length; i++)
            {
                var weights = _ranges[i].Parameters?.BalloonWeights;
                var hasPositiveWeight = false;
                if (weights != null)
                {
                    foreach (var weight in weights)
                    {
                        if (weight.Weight > 0f)
                        {
                            hasPositiveWeight = true;
                            break;
                        }
                    }
                }

                if (!hasPositiveWeight)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): range starting at {_ranges[i].FromLevel} has no " +
                        "balloon type with a positive weight — nothing could spawn.");
                }
            }
        }

        private void WarnOnNonMonotonicThreshold()
        {
            var lastOverrideLevel = 0;
            foreach (var entry in _thresholdOverrides)
            {
                lastOverrideLevel = Mathf.Max(lastOverrideLevel, entry.ToLevel);
            }

            // The formula tail is inherently increasing, so only the override ranges can break monotonicity —
            // check across them and a couple levels past into the formula.
            var lastLevel = Mathf.Max(2, lastOverrideLevel + 2);
            var previous = int.MinValue;

            for (var level = 1; level <= lastLevel; level++)
            {
                var composed = ThresholdForLevel(level);
                if (composed <= 0)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): threshold at level {level} is non-positive " +
                        $"({composed}) — check the override milestones.");
                }
                else if (composed < previous)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): threshold drops at level {level} " +
                        $"({previous} → {composed}) — keep each override's cumulative curve increasing per level.");
                }

                previous = composed;
            }
        }
#endif
    }
}
