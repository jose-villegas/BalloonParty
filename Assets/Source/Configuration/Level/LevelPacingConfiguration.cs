using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Per-colour points-required per level. A covering <see cref="_thresholdOverrides" /> entry sets the
    /// level's cumulative run-score milestone and the per-colour bar is the delta from the previous level split
    /// across that level's colours; uncovered levels use the formula (<see cref="_baseValue" /> + logarithmic
    /// growth, scaled by <see cref="_thresholdCurve" />).</summary>
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

        [Tooltip("Per-level multiplier over the base scaling formula (base value + logarithmic growth). " +
                 "X = level, Y = multiplier; flat 1 = the pure formula. Decoupled from the absolute scale, " +
                 "which the base value sets — so keep values near 1, not raw scores.")]
        [FormerlySerializedAs("_thresholdModifier")]
        [SerializeField] private AnimationCurve _thresholdCurve = AnimationCurve.Constant(1f, 100f, 1f);

        [Tooltip("Cap each level's points-required DOWN to a multiple of this (e.g. 50 or 70) for clean " +
                 "targets — 732 caps to 700, not 750. 0 or 1 = no capping.")]
        [SerializeField] private int _thresholdRounding = 50;

        public IReadOnlyList<LevelRangeEntry> Ranges => _ranges;

        private void OnValidate()
        {
#if UNITY_EDITOR
            SortRangesByFromLevel();
            WarnOnGapsAndOverlaps();
            WarnOnMissingOrMultipleTails();
            WarnOnEmptyWeightedSets();
            WarnOnNonMonotonicThreshold();
#endif
        }

        // An override sets this level's cumulative run-score milestone; the per-colour bar is the increment over
        // the previous level's milestone, split across this level's colours. Uncovered levels use the formula:
        // base + logarithmic growth, scaled by the multiplier curve, then snapped to a clean multiple.
        public int ThresholdForLevel(int level)
        {
            if (TryGetOverride(level, out var entry))
            {
                var increment = entry.CumulativeScore(level) - CumulativeScoreForLevel(level - 1);
                return Mathf.Max(1, Mathf.RoundToInt(increment / ColorsForLevel(level)));
            }

            var scaling = _baseValue + Mathf.Exp(2f) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI));
            var multiplier = _thresholdCurve.Evaluate(level);
            if (multiplier <= 0f)
            {
                multiplier = 1f;
            }

            return RoundThreshold(Mathf.RoundToInt(scaling * multiplier));
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
                if (entry.Contains(level))
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
        private void SortRangesByFromLevel()
        {
            Array.Sort(_ranges, (a, b) => a.FromLevel.CompareTo(b.FromLevel));
        }

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
            var lastKeyTime = 0f;
            foreach (var key in _thresholdCurve.keys)
            {
                lastKeyTime = Mathf.Max(lastKeyTime, key.time);
            }

            // X is level-1, and the exponent holds past the last key — check a bit beyond it.
            var lastLevel = Mathf.Max(2, Mathf.CeilToInt(lastKeyTime) + 2);
            var previous = int.MinValue;

            for (var level = 1; level <= lastLevel; level++)
            {
                var composed = ThresholdForLevel(level);
                if (composed <= 0)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): threshold at level {level} is non-positive " +
                        $"({composed}) — check the threshold curve.");
                }
                else if (composed < previous)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): threshold drops at level {level} " +
                        $"({previous} → {composed}) — keep the curve's exponents ≥ 1 so it's non-decreasing.");
                }

                previous = composed;
            }
        }
#endif
    }
}
