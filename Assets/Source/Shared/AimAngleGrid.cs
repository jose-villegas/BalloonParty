using BalloonParty.Thrower;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The thrower's reachable-angle domain rule: the absolute step grid a quantized aim snaps to,
    ///     and the sample grid a sweep over an arc uses to visit exactly those angles. One rule with
    ///     several independent consumers — the live <see cref="ThrowerController" />, the Fire Best Shot
    ///     cheat, the editor Shot Solver window, and the Scene-view aim fan overlay — so it lives here
    ///     rather than inside any one of them.
    /// </summary>
    internal static class AimAngleGrid
    {
        /// <summary>How many angles a sweep over [<paramref name="arcMinDegrees" />,
        /// <paramref name="arcMaxDegrees" />] needs. Continuous aim (<paramref name="stepDegrees" />
        /// &lt;= 0) keeps the caller's fixed <paramref name="continuousSampleCount" /> unchanged —
        /// today's behaviour. A quantized aim instead counts the step multiples that land inside the
        /// arc, so the sweep only ever visits angles
        /// <see cref="ThrowerController.ClampAndQuantizeAimDirection" /> can actually reach — a ~31° arc
        /// at a 5° step is ~7 samples, not thousands. Always at least
        /// 1, even when the step is wider than the whole arc.</summary>
        internal static int ResolveSweepSampleCount(
            float arcMinDegrees, float arcMaxDegrees, float stepDegrees, int continuousSampleCount)
        {
            if (stepDegrees <= 0f)
            {
                return Mathf.Max(1, continuousSampleCount);
            }

            var firstIndex = GridFirstIndex(arcMinDegrees, stepDegrees);
            var lastIndex = GridLastIndex(arcMaxDegrees, stepDegrees);
            return firstIndex <= lastIndex ? lastIndex - firstIndex + 1 : 1;
        }

        /// <summary>The i'th sweep angle over [<paramref name="arcMinDegrees" />,
        /// <paramref name="arcMaxDegrees" />], paired with <see cref="ResolveSweepSampleCount" />.
        /// Continuous aim (<paramref name="stepDegrees" /> &lt;= 0) lerps evenly across
        /// <paramref name="continuousSampleCount" /> samples, exactly as before quantization existed.
        /// A quantized aim instead walks the absolute step grid — the same grid
        /// <see cref="ThrowerController.ClampAndQuantizeAimDirection" /> snaps player input to — rather than
        /// lerping and hoping the result coincides with a reachable angle. That grid is anchored at
        /// multiples of <paramref name="stepDegrees" /> in absolute terms, not at
        /// <paramref name="arcMinDegrees" />, so the first sample is the smallest step multiple at or
        /// above <paramref name="arcMinDegrees" /> — not necessarily <paramref name="arcMinDegrees" />
        /// itself. When the step is wider than the whole arc (no multiple falls inside it), this
        /// returns whichever neighbouring grid line sits closest to the arc, so the single guaranteed
        /// sample from <see cref="ResolveSweepSampleCount" /> is still a reachable angle.</summary>
        internal static float ResolveSweepAngle(
            int index, float arcMinDegrees, float arcMaxDegrees, float stepDegrees, int continuousSampleCount)
        {
            if (stepDegrees <= 0f)
            {
                var count = Mathf.Max(1, continuousSampleCount);
                var t = count <= 1 ? 0f : index / (float)(count - 1);
                return Mathf.Lerp(arcMinDegrees, arcMaxDegrees, t);
            }

            var firstIndex = GridFirstIndex(arcMinDegrees, stepDegrees);
            var lastIndex = GridLastIndex(arcMaxDegrees, stepDegrees);
            if (firstIndex <= lastIndex)
            {
                return (firstIndex + index) * stepDegrees;
            }

            var below = lastIndex * stepDegrees;
            var above = firstIndex * stepDegrees;
            return arcMinDegrees - below <= above - arcMaxDegrees ? below : above;
        }

        /// <summary>Resolves a raw aim angle into the thrower's reachable set: clamped into
        /// [<paramref name="minDegrees" />, <paramref name="maxDegrees" />] and, for a quantized aim
        /// (<paramref name="stepDegrees" /> &gt; 0), snapped onto the exact same absolute step grid
        /// <see cref="ResolveSweepAngle" /> sweeps — so the thrower's own clamp and every sweep built on
        /// this grid can never disagree about what angles are reachable. Order matters: clamping the
        /// angle and then snapping can push the result back outside the range (the nearest grid line to
        /// a clamped boundary sample can sit past it); snapping the angle and then clamping it can land
        /// on a value that isn't a step multiple at all. This sidesteps both by clamping the ROUNDED
        /// GRID INDEX instead of the angle, so the result is always both a step multiple and inside
        /// the range, by construction. Continuous aim (<paramref name="stepDegrees" /> &lt;= 0) is a
        /// plain clamp. When no step multiple falls inside the range at all (a step wider than the
        /// whole range), falls back to whichever neighbouring grid line sits closest — mirroring
        /// <see cref="ResolveSweepAngle" />'s identical fallback for the same degenerate case. Called
        /// directly by <see cref="ThrowerController.ClampAndQuantizeAimDirection" />.</summary>
        internal static float ClampToReachableAngle(
            float rawAngleDegrees, float minDegrees, float maxDegrees, float stepDegrees)
        {
            if (stepDegrees <= 0f)
            {
                return Mathf.Clamp(rawAngleDegrees, minDegrees, maxDegrees);
            }

            var firstIndex = GridFirstIndex(minDegrees, stepDegrees);
            var lastIndex = GridLastIndex(maxDegrees, stepDegrees);
            if (firstIndex > lastIndex)
            {
                var below = lastIndex * stepDegrees;
                var above = firstIndex * stepDegrees;
                return minDegrees - below <= above - maxDegrees ? below : above;
            }

            var rawIndex = Mathf.RoundToInt(rawAngleDegrees / stepDegrees);
            var clampedIndex = Mathf.Clamp(rawIndex, firstIndex, lastIndex);
            return clampedIndex * stepDegrees;
        }

        // Smallest step multiple at or above arcMinDegrees — the grid is anchored at absolute
        // multiples of the step, not at the arc bounds.
        private static int GridFirstIndex(float arcMinDegrees, float stepDegrees)
        {
            return Mathf.CeilToInt(arcMinDegrees / stepDegrees);
        }

        // Largest step multiple at or below arcMaxDegrees.
        private static int GridLastIndex(float arcMaxDegrees, float stepDegrees)
        {
            return Mathf.FloorToInt(arcMaxDegrees / stepDegrees);
        }
    }
}
