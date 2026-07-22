using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Per-colour points-required per level. The <see cref="_scoringCurve"/> defines cumulative score
    /// milestones; the per-colour bar at each level is the delta from the previous milestone split across that
    /// level's active colours, rounded to a clean multiple.</summary>
    [CreateAssetMenu(menuName = "Configuration/Level Pacing", fileName = "LevelPacingConfiguration")]
    internal class LevelPacingConfiguration : ScriptableObject, ILevelPacingConfiguration
    {
        [SerializeField] private LevelRangeEntry[] _ranges =
        {
            new(0, 0, new RangedLevelParameters()),
        };

        [Tooltip("Cap each level's points-required DOWN to a multiple of this (e.g. 50 or 70) for clean " +
                 "targets — 732 caps to 700, not 750. 0 or 1 = no capping.")]
        [SerializeField] private int _thresholdRounding = 50;

        [Tooltip("Unified scoring curve — authors cumulative milestones at key levels; intermediate levels are " +
                 "interpolated via Fritsch–Carlson monotone cubic; beyond the last point the tail extrapolates.")]
        [SerializeField] private LevelScoringCurve _scoringCurve;

        public IReadOnlyList<LevelRangeEntry> Ranges => _ranges;

        private void OnValidate()
        {
#if UNITY_EDITOR
            _scoringCurve.Validate(name);
            WarnOnGapsAndOverlaps();
            WarnOnFallbackIssues();
            WarnOnEmptyWeightedSets();
            WarnOnNonMonotonicThreshold();
#endif
        }

        public int ThresholdForLevel(int level)
        {
            var cumThis = _scoringCurve.CumulativeMilestone(level);
            var cumPrev = _scoringCurve.CumulativeMilestone(level - 1);
            var increment = cumThis - cumPrev;
            var perColor = Mathf.RoundToInt(increment / ColorsForLevel(level));
            return Mathf.Max(1, RoundThreshold(perColor));
        }

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

                    var cap = weight.MaxCount > 0
                        ? weight.MaxCount
                        : columns * parameters.BoardLines;
                    max = Mathf.Max(max, cap);
                }
            }

            return max;
        }

        internal int ColorsForLevel(int level)
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

        private int MaskForLevel(int level)
        {
            var fallbackMask = 0;

            for (var i = 0; i < _ranges.Length; i++)
            {
                if (_ranges[i].IsFallback)
                {
                    fallbackMask = _ranges[i].Parameters?.AllowedColorsMask ?? 0;
                    continue;
                }

                if (_ranges[i].Contains(level))
                {
                    return _ranges[i].Parameters?.AllowedColorsMask ?? 0;
                }
            }

            return fallbackMask;
        }

#if UNITY_EDITOR
        private void WarnOnGapsAndOverlaps()
        {
            for (var i = 1; i < _ranges.Length; i++)
            {
                var previous = _ranges[i - 1];
                var current = _ranges[i];

                if (previous.IsFallback || current.IsFallback)
                {
                    continue;
                }

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

        private void WarnOnFallbackIssues()
        {
            var hasDefault = false;
            var seenIds = new System.Collections.Generic.HashSet<int>();

            for (var i = 0; i < _ranges.Length; i++)
            {
                if (!_ranges[i].IsFallback)
                {
                    continue;
                }

                if (_ranges[i].FromLevel == -1)
                {
                    hasDefault = true;
                }

                if (!seenIds.Add(_ranges[i].FromLevel))
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): duplicate fallback ID {_ranges[i].FromLevel} " +
                        "— each fallback must have a unique FromLevel.");
                }
            }

            if (!hasDefault)
            {
                Debug.LogWarning(
                    $"LevelPacingConfiguration ({name}): missing default fallback (FromLevel = -1). " +
                    "Normal gameplay requires exactly one entry with FromLevel = -1.");
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
            if (_scoringCurve.IsEmpty)
            {
                return;
            }

            var previous = int.MinValue;
            var checkLevels = 50;

            for (var level = 1; level <= checkLevels; level++)
            {
                var composed = ThresholdForLevel(level);
                if (composed <= 0)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): threshold at level {level} is non-positive " +
                        $"({composed}) — check the scoring curve milestones.");
                }
                else if (composed < previous)
                {
                    Debug.LogWarning(
                        $"LevelPacingConfiguration ({name}): threshold drops at level {level} " +
                        $"({previous} → {composed}) — ensure the cumulative curve is increasing.");
                }

                previous = composed;
            }
        }
#endif
    }
}
