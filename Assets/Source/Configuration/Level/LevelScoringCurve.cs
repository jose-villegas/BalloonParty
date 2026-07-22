using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Monotone piecewise-cubic scoring curve. Authors cumulative score milestones at key levels;
    /// intermediate levels are interpolated via Fritsch–Carlson monotone Hermite (with cached tangents to
    /// avoid per-call allocation); levels beyond the last control point extrapolate via
    /// <see cref="TailGrowthConfig"/>. Per-segment mode overrides allow Linear, Convex, or Concave shapes.
    /// <para>A difficulty spike requires three control points: rise, peak, and return.</para></summary>
    [Serializable]
    internal struct LevelScoringCurve
    {
        [SerializeField] private ScoringControlPoint[] _controlPoints;
        [SerializeField] private TailGrowthConfig _tailGrowth;

        [NonSerialized] private float[] _cachedTangents;
        [NonSerialized] private int _cachedTangentsVersion;

        public IReadOnlyList<ScoringControlPoint> ControlPoints => _controlPoints;
        public TailGrowthConfig TailGrowth => _tailGrowth;
        public bool IsEmpty => _controlPoints == null || _controlPoints.Length == 0;

        internal LevelScoringCurve(ScoringControlPoint[] points, TailGrowthConfig tail)
        {
            _controlPoints = points;
            _tailGrowth = tail;
            _cachedTangents = null;
            _cachedTangentsVersion = 0;
        }

        /// <summary>Cumulative milestone for a given level. Total function over [0, ∞):
        /// level ≤ 0 returns 0; level &lt; first CP ramps linearly from origin; between CPs uses
        /// Fritsch–Carlson monotone cubic; beyond last CP extrapolates via tail config.</summary>
        public float CumulativeMilestone(int level)
        {
            if (_controlPoints == null || _controlPoints.Length == 0)
            {
                return 0f;
            }

            if (level <= 0)
            {
                return 0f;
            }

            var first = _controlPoints[0];
            var last = _controlPoints[_controlPoints.Length - 1];

            if (level < first.Level)
            {
                return first.CumulativeScore * ((float)level / first.Level);
            }

            if (level > last.Level)
            {
                return ExtrapolateTail(level);
            }

            // Single CP: the only remaining case is level == first.Level.
            if (_controlPoints.Length == 1)
            {
                return first.CumulativeScore;
            }

            return EvaluateFritschCarlson(level);
        }

        /// <summary>Fritsch–Carlson monotone piecewise cubic interpolation. Guarantees the interpolated
        /// values stay between adjacent control-point scores (no overshooting).
        /// Per-segment mode overrides allow Linear, Convex, or Concave interpolation.</summary>
        private float EvaluateFritschCarlson(int level)
        {
            var n = _controlPoints.Length;

            // Find the segment [seg, seg+1] containing the level.
            var seg = n - 2;
            for (var i = 0; i < n - 1; i++)
            {
                if (level <= _controlPoints[i + 1].Level)
                {
                    seg = i;
                    break;
                }
            }

            // If level lands exactly on a CP, return its value.
            if (level == _controlPoints[seg].Level)
            {
                return _controlPoints[seg].CumulativeScore;
            }

            if (seg < n - 1 && level == _controlPoints[seg + 1].Level)
            {
                return _controlPoints[seg + 1].CumulativeScore;
            }

            var x0 = (float)_controlPoints[seg].Level;
            var x1 = (float)_controlPoints[seg + 1].Level;
            var y0 = _controlPoints[seg].CumulativeScore;
            var y1 = _controlPoints[seg + 1].CumulativeScore;
            var segH = x1 - x0;
            var t = (level - x0) / segH;

            // Dispatch based on the segment mode defined on the starting CP.
            var mode = _controlPoints[seg].SegmentMode;
            switch (mode)
            {
                case SegmentMode.Linear:
                    return y0 + (y1 - y0) * t;

                case SegmentMode.Convex:
                {
                    // Quadratic ease-in: starts slow, ends fast.
                    var t2 = t * t;
                    return y0 + (y1 - y0) * t2;
                }

                case SegmentMode.Concave:
                {
                    // Quadratic ease-out: starts fast, ends slow.
                    var tInv = 1f - t;
                    return y0 + (y1 - y0) * (1f - tInv * tInv);
                }

                case SegmentMode.Smooth:
                default:
                    return EvaluateSmoothSegment(seg, t, segH, y0, y1);
            }
        }

        /// <summary>Smooth (Fritsch–Carlson) evaluation for a single segment. Uses cached tangents
        /// to avoid per-call allocations.</summary>
        private float EvaluateSmoothSegment(int seg, float t, float segH, float y0, float y1)
        {
            var tangents = GetOrBuildTangents();

            // Evaluate the cubic Hermite on the found segment.
            var m0 = tangents[seg];
            var m1 = tangents[seg + 1];

            var t2 = t * t;
            var t3 = t2 * t;

            // Hermite basis functions.
            var h00 = 2f * t3 - 3f * t2 + 1f;
            var h10 = t3 - 2f * t2 + t;
            var h01 = -2f * t3 + 3f * t2;
            var h11 = t3 - t2;

            return h00 * y0 + h10 * segH * m0 + h01 * y1 + h11 * segH * m1;
        }

        /// <summary>Returns cached Fritsch–Carlson tangents, rebuilding if control points changed.</summary>
        private float[] GetOrBuildTangents()
        {
            var version = _controlPoints?.Length ?? 0;
            if (_cachedTangents != null && _cachedTangentsVersion == version)
            {
                return _cachedTangents;
            }

            var n = _controlPoints.Length;

            // Compute secant slopes (deltas) between adjacent CPs.
            var deltas = new float[n - 1];
            for (var i = 0; i < n - 1; i++)
            {
                var h = (float)(_controlPoints[i + 1].Level - _controlPoints[i].Level);
                deltas[i] = h > 0
                    ? (_controlPoints[i + 1].CumulativeScore - _controlPoints[i].CumulativeScore) / h
                    : 0f;
            }

            // Compute tangents using Fritsch–Carlson conditions for monotonicity.
            var tangents = new float[n];
            tangents[0] = deltas[0];
            tangents[n - 1] = deltas[n - 2];

            for (var i = 1; i < n - 1; i++)
            {
                if (deltas[i - 1] * deltas[i] <= 0f)
                {
                    tangents[i] = 0f;
                }
                else
                {
                    tangents[i] = 2f * deltas[i - 1] * deltas[i] / (deltas[i - 1] + deltas[i]);
                }
            }

            // Fritsch–Carlson monotonicity enforcement: clamp tangent magnitudes.
            for (var i = 0; i < n - 1; i++)
            {
                if (Mathf.Approximately(deltas[i], 0f))
                {
                    tangents[i] = 0f;
                    tangents[i + 1] = 0f;
                    continue;
                }

                var alpha = tangents[i] / deltas[i];
                var beta = tangents[i + 1] / deltas[i];

                var sqSum = alpha * alpha + beta * beta;
                if (sqSum > 9f)
                {
                    var tau = 3f / Mathf.Sqrt(sqSum);
                    tangents[i] = tau * alpha * deltas[i];
                    tangents[i + 1] = tau * beta * deltas[i];
                }
            }

            _cachedTangents = tangents;
            _cachedTangentsVersion = version;
            return tangents;
        }

        /// <summary>Extrapolates beyond the last control point using the configured tail growth mode.
        /// The base increment is derived from the last two CPs (or from the last CP and the one before it
        /// via the pre-ramp if only one CP exists).</summary>
        private float ExtrapolateTail(int level)
        {
            var n = _controlPoints.Length;
            var last = _controlPoints[n - 1];

            // Determine the base increment (last segment's average per-level growth).
            float baseIncrement;
            if (n >= 2)
            {
                var prev = _controlPoints[n - 2];
                var span = last.Level - prev.Level;
                baseIncrement = span > 0
                    ? (last.CumulativeScore - prev.CumulativeScore) / span
                    : last.CumulativeScore;
            }
            else
            {
                // Single CP: derive increment from the linear ramp (origin to CP).
                baseIncrement = last.Level > 0 ? last.CumulativeScore / last.Level : last.CumulativeScore;
            }

            baseIncrement = Mathf.Max(0f, baseIncrement);
            var levelsOut = level - last.Level;
            var rate = Mathf.Max(0f, _tailGrowth.Rate);

            switch (_tailGrowth.Mode)
            {
                case TailGrowthMode.Geometric:
                {
                    // Geometric series on the increment: each level's increment = baseIncrement * rate^i.
                    // rate = 1 means constant increment (flat growth).
                    if (Mathf.Approximately(rate, 1f))
                    {
                        return last.CumulativeScore + baseIncrement * levelsOut;
                    }

                    // Sum of geometric series: base * (rate^n - 1) / (rate - 1)
                    var geometricSum = baseIncrement * (Mathf.Pow(rate, levelsOut) - 1f) / (rate - 1f);
                    return last.CumulativeScore + geometricSum;
                }
                case TailGrowthMode.Linear:
                {
                    // Arithmetic series: increment grows by 'rate' each level.
                    // Level i past last CP has increment = baseIncrement + rate * i.
                    // Sum = levelsOut * baseIncrement + rate * levelsOut * (levelsOut - 1) / 2.
                    var arithmeticSum = levelsOut * baseIncrement + rate * levelsOut * (levelsOut - 1) / 2f;
                    return last.CumulativeScore + arithmeticSum;
                }
                default:
                    return last.CumulativeScore + baseIncrement * levelsOut;
            }
        }

#if UNITY_EDITOR
        /// <summary>Validates control points are sorted and monotonically non-decreasing.
        /// Sorts in-place and logs warnings for non-monotonic cumulative scores.</summary>
        internal void Validate(string assetName)
        {
            if (_controlPoints == null || _controlPoints.Length == 0)
            {
                return;
            }

            // Invalidate tangent cache since control points may have changed.
            _cachedTangents = null;

            // Sort by level (stable — preserves order for equal levels; last-wins via evaluation).
            Array.Sort(_controlPoints, (a, b) => a.Level.CompareTo(b.Level));

            for (var i = 1; i < _controlPoints.Length; i++)
            {
                if (_controlPoints[i].CumulativeScore < _controlPoints[i - 1].CumulativeScore)
                {
                    Debug.LogWarning(
                        $"LevelScoringCurve ({assetName}): cumulative score at level {_controlPoints[i].Level} " +
                        $"({_controlPoints[i].CumulativeScore}) is less than at level " +
                        $"{_controlPoints[i - 1].Level} ({_controlPoints[i - 1].CumulativeScore}) — " +
                        "the curve must be non-decreasing.");
                }
            }

            if (_tailGrowth.Rate < 0f)
            {
                Debug.LogWarning(
                    $"LevelScoringCurve ({assetName}): tail growth rate is negative ({_tailGrowth.Rate}) — " +
                    "clamping to 0 at runtime.");
            }
        }
#endif
    }
}
