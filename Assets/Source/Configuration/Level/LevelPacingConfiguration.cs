using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Threshold modifier is a dimensionless multiplier over the base formula, so "20% cheaper" means the same at every level.</summary>
    [CreateAssetMenu(menuName = "Configuration/Level Pacing", fileName = "LevelPacingConfiguration")]
    internal class LevelPacingConfiguration : ScriptableObject, ILevelPacingConfiguration
    {
        [SerializeField] private LevelRangeEntry[] _ranges =
        {
            // Construct Parameters explicitly — the struct default zero-initializes it to null.
            new(0, 0, new RangedLevelParameters()),
        };

        [Tooltip("Dimensionless multiplier over the points-required-for-level formula. Flat 1.0 = no effect.")]
        [SerializeField] private AnimationCurve _thresholdModifier = AnimationCurve.Constant(1, 100, 1f);

        [Tooltip("Round each level's points-required to the nearest multiple of this (e.g. 50 or 70) for " +
                 "clean targets. 0 or 1 = no rounding.")]
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

        public float ThresholdModifier(int level)
        {
            var value = _thresholdModifier.Evaluate(level);
            return value > 0f ? value : 1f;
        }

        // Snap to the nearest multiple, floored at one multiple so a level never rounds to zero.
        public int RoundThreshold(int rawPoints)
        {
            if (_thresholdRounding <= 1)
            {
                return rawPoints;
            }

            return Mathf.Max(_thresholdRounding, Mathf.RoundToInt(rawPoints / (float)_thresholdRounding) * _thresholdRounding);
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

        // Duplicated, not injected: OnValidate runs in the editor with no DI container.
        private static int BaseFormula(int level)
        {
            return (int)((Mathf.Exp(2) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI))) + 25f);
        }

        private void WarnOnNonMonotonicThreshold()
        {
            var lastKeyTime = 0f;
            foreach (var key in _thresholdModifier.keys)
            {
                lastKeyTime = Mathf.Max(lastKeyTime, key.time);
            }

            var lastLevel = Mathf.Max(1, Mathf.CeilToInt(lastKeyTime));
            var previous = int.MinValue;

            for (var level = 1; level <= lastLevel; level++)
            {
                var composed = RoundThreshold(Mathf.RoundToInt(BaseFormula(level) * ThresholdModifier(level)));
                if (composed <= 0)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): composed threshold at level {level} is non-positive " +
                        $"({composed}) — check the modifier curve.");
                }
                else if (composed < previous)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): composed threshold drops at level {level} " +
                        $"({previous} → {composed}) — the modifier curve must keep it non-decreasing.");
                }

                previous = composed;
            }
        }
#endif
    }
}
