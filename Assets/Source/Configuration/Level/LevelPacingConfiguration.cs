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
        internal LevelScoringCurve ScoringCurve => _scoringCurve;

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

        public int ColorsForLevel(int level)
        {
            var bits = (uint)MaskForLevel(level);
            var count = 0;
            while (bits != 0)
            {
                count += (int)(bits & 1);
                bits >>= 1;
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
    }
}
