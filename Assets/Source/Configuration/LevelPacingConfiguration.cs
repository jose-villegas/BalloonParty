using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Level-range difficulty authoring: ordered contiguous <see cref="LevelRangeEntry" />s (the
    ///     last is open-ended), exact-level <see cref="CustomLevelEntry" /> overlays, and the global
    ///     level-up threshold modifier curve. The curve composes <b>multiplicatively</b> with the base
    ///     points-required formula (<c>required(level) = round(formula(level) × modifier)</c>) — a
    ///     dimensionless multiplier, not an additive offset, so "20% cheaper" means the same thing at
    ///     level 1 and level 20. Default flat 1.0 = pure formula, zero effect until keys are authored.
    /// </summary>
    [CreateAssetMenu(menuName = "Configuration/Level Pacing", fileName = "LevelPacingConfiguration")]
    internal class LevelPacingConfiguration : ScriptableObject, ILevelPacingConfiguration
    {
        [SerializeField] private LevelRangeEntry[] _ranges =
        {
            // A struct's default constructor zero-initializes Parameters to null — construct it
            // explicitly so a fresh instance is actually valid (see LevelRangeEntry's ctor doc).
            new(0, 0, new RangedLevelParameters()),
        };

        [SerializeField] private CustomLevelEntry[] _customLevels = Array.Empty<CustomLevelEntry>();

        [Tooltip("Dimensionless multiplier over the points-required-for-level formula. Flat 1.0 = no effect.")]
        [SerializeField] private AnimationCurve _thresholdModifier = AnimationCurve.Constant(1, 100, 1f);

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

        public IReadOnlyList<LevelRangeEntry> Ranges => _ranges;
        public IReadOnlyList<CustomLevelEntry> CustomLevels => _customLevels;

        public float ThresholdModifier(int level)
        {
            var value = _thresholdModifier.Evaluate(level);
            return value > 0f ? value : 1f;
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

        // Duplicated (not injected) intentionally: OnValidate runs in the editor with no DI container,
        // and the formula is a fixed constant of the game (see GameConfiguration.PointsRequiredForLevel) —
        // this is a validation-time mirror, not a second source of truth for runtime.
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
                var composed = Mathf.RoundToInt(BaseFormula(level) * ThresholdModifier(level));
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
