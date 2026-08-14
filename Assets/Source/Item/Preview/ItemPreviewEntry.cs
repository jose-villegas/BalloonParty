using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     Pure polyline-vs-polyline math: where a trace first crosses one stroke of an
    ///     <see cref="ItemPreviewShape" />, expressed as an arc-length offset along that stroke. Factored out
    ///     so the intersection walk is edit-mode testable without the ticker's pen/pool machinery, mirroring
    ///     <see cref="BalloonParty.Prediction.TraceHitGeometry" />.
    /// </summary>
    internal static class ItemPreviewEntry
    {
        // Threshold on the segment-pair cross product (twice the parallelogram area the two segments
        // span) below which they're treated as parallel — guards the intersection divide rather than
        // letting a near-zero denominator blow the result up.
        private const float ParallelEpsilon = 1e-6f;

        /// <summary>
        ///     Finds where <paramref name="tracePoints" /> first crosses <paramref name="shape" />'s stroke
        ///     at <paramref name="strokeIndex" />, walking the trace segment by segment so "first" means
        ///     earliest along the shot's own direction of travel rather than merely the first stroke
        ///     segment tested. A closed stroke's wrap leg (last point back to first) counts as a segment.
        ///     Ties within one trace segment — more than one stroke segment crossed at once — are broken by
        ///     the intersection nearest that trace segment's own start.
        /// </summary>
        /// <param name="arcTable">
        ///     The caller's cumulative-arc-length table, parallel to <see cref="ItemPreviewShape.Points" />:
        ///     <c>arcTable[stroke.Start + i]</c> is the distance from the stroke's first point to its i-th
        ///     (see <c>ItemPreviewTicker.BuildArcTable</c>). Read only, never rebuilt here.
        /// </param>
        /// <param name="offset">
        ///     The arc length from the stroke's first point to the crossing, along the stroke itself (not
        ///     the trace). Zero when this returns false.
        /// </param>
        /// <param name="traceOffset">
        ///     The arc length from the trace's own first point to the crossing, measured along the trace
        ///     polyline — lets a caller (the leading pen's approach) travel the trace itself rather than
        ///     cut a straight line to the entry. Zero when this returns false.
        /// </param>
        internal static bool TryFindEntryOffset(
            ItemPreviewShape shape,
            int strokeIndex,
            IReadOnlyList<float> arcTable,
            IReadOnlyList<Vector3> tracePoints,
            out float offset,
            out float traceOffset)
        {
            offset = 0f;
            traceOffset = 0f;

            if (shape == null || tracePoints == null || tracePoints.Count < 2)
            {
                return false;
            }

            if (strokeIndex < 0 || strokeIndex >= shape.Strokes.Count)
            {
                return false;
            }

            var stroke = shape.Strokes[strokeIndex];
            if (stroke.Count < 2)
            {
                return false;
            }

            var points = shape.Points;
            var segmentCount = stroke.Closed ? stroke.Count : stroke.Count - 1;

            var traceArc = 0f;
            for (var traceIndex = 0; traceIndex < tracePoints.Count - 1; traceIndex++)
            {
                var p1 = tracePoints[traceIndex];
                var p2 = tracePoints[traceIndex + 1];
                var traceSegmentLength = Vector3.Distance(p1, p2);

                if (!TryFindNearestCrossing(
                        p1, p2, stroke, points, segmentCount, out var segment, out var tStroke, out var tTrace))
                {
                    traceArc += traceSegmentLength;
                    continue;
                }

                offset = ComputeArcOffset(stroke, points, arcTable, segment, tStroke);
                traceOffset = traceArc + (tTrace * traceSegmentLength);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Where the closest point of an arbitrary polyline to <paramref name="target" /> sits: the
        ///     point itself and its arc-length offset from the polyline's own first point. Shared by
        ///     <see cref="ItemPreviewTicker" /> (locating the host on the aim trace, to anchor the
        ///     approach cascade's own start) and <see cref="SnipeRangePreview" /> (anchoring the pierce
        ///     corridor on that same trace instead of the host centre) so this projection exists in one
        ///     place rather than twice. An O(n) walk over every segment, fine for either caller: the
        ///     ticker runs it once per <c>Show</c>/refit, never per frame, and Snipe once per
        ///     <c>BuildShape</c>.
        /// </summary>
        /// <returns>
        ///     False when <paramref name="points" /> has fewer than two points to project onto — nothing
        ///     to be nearest to — leaving <paramref name="point" /> and <paramref name="offset" /> at
        ///     their defaults for the caller's own fallback.
        /// </returns>
        internal static bool TryFindNearestPointOnPolyline(
            IReadOnlyList<Vector3> points, Vector3 target, out Vector3 point, out float offset)
        {
            point = default;
            offset = 0f;

            if (points == null || points.Count < 2)
            {
                return false;
            }

            var bestDistanceSqr = float.MaxValue;
            var accumulated = 0f;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var segment = points[i + 1] - a;
                var segmentLengthSqr = segment.sqrMagnitude;
                var segmentLength = Mathf.Sqrt(segmentLengthSqr);

                var t = segmentLengthSqr <= 1e-10f
                    ? 0f
                    : Mathf.Clamp01(Vector3.Dot(target - a, segment) / segmentLengthSqr);

                var candidate = a + (segment * t);
                var distanceSqr = (target - candidate).sqrMagnitude;
                if (distanceSqr < bestDistanceSqr)
                {
                    bestDistanceSqr = distanceSqr;
                    offset = accumulated + (t * segmentLength);
                    point = candidate;
                }

                accumulated += segmentLength;
            }

            return true;
        }

        /// <summary>
        ///     Appends a closed loop around <paramref name="center" /> at <paramref name="radius" /> to
        ///     <paramref name="buffer" /> — the "circle the balloon once" flourish
        ///     <see cref="ItemPreviewTicker" /> prefixes onto an approach leg, ahead of the leg's own
        ///     points. Starts and ends at the very same vertex: the last point re-uses the first outright
        ///     rather than recomputing it from angle + 2π, so the loop closes exactly regardless of trig
        ///     round-off. That starting vertex sits on the ray from <paramref name="center" /> through
        ///     <paramref name="towardPoint" /> — the direction the leg that follows already departs in —
        ///     so the loop's own end and the leg's own start connect via the shortest possible bridge
        ///     rather than an arbitrary one; the caller (<c>ItemPreviewTicker.BuildApproachPaths</c>)
        ///     appends the leg's true first point right after, and the two are simply adjacent points in
        ///     one polyline from there — no special-casing needed at that seam, only at this one.
        /// </summary>
        /// <param name="fallbackDirection">
        ///     Used when <paramref name="towardPoint" /> coincides with <paramref name="center" /> — true
        ///     of Laser, Paint and Lightning's first arc, whose entry sits on the host itself, leaving no
        ///     leg direction to sight the loop's start off. Normally the aim's own travel direction.
        /// </param>
        /// <returns>
        ///     The loop's own arc length: the discrete polygon's actual perimeter, summed the same way
        ///     every other stroke length in this file is (consecutive point distances), not the ideal
        ///     <c>2πr</c> — so it stays exactly consistent with how the caller's own arc-length table
        ///     later walks these same points. Zero (nothing appended) for a non-positive radius or fewer
        ///     than 3 segments, the caller's own "no loop" case.
        /// </returns>
        internal static float AppendApproachLoop(
            List<Vector3> buffer, Vector3 center, Vector3 towardPoint, Vector2 fallbackDirection, float radius,
            int segments)
        {
            if (radius <= 0f || segments < 3)
            {
                return 0f;
            }

            var direction = new Vector2(towardPoint.x - center.x, towardPoint.y - center.y);
            if (direction.sqrMagnitude <= 1e-8f)
            {
                direction = fallbackDirection;
            }

            if (direction.sqrMagnitude <= 1e-8f)
            {
                direction = Vector2.right;
            }

            direction.Normalize();

            // Counter-clockwise (increasing angle), matching ItemPreviewShape.AddCircle's own winding —
            // the figure's own circles (Bomb's blast radius) and this flourish read as one consistent
            // system rather than two circles that happen to spin opposite ways.
            var startAngle = Mathf.Atan2(direction.y, direction.x);
            var step = Mathf.PI * 2f / segments;
            var first = center + new Vector3(direction.x * radius, direction.y * radius, 0f);

            buffer.Add(first);

            var length = 0f;
            var previous = first;
            for (var i = 1; i < segments; i++)
            {
                var angle = startAngle + (step * i);
                var point = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                length += Vector3.Distance(previous, point);
                buffer.Add(point);
                previous = point;
            }

            length += Vector3.Distance(previous, first);
            buffer.Add(first);

            return length;
        }

        /// <summary>
        ///     Where <paramref name="tracePoints" /> crosses the circle of <paramref name="radius" /> around
        ///     <paramref name="center" />, walking outward from <paramref name="hostOffset" /> — the centre's
        ///     own arc-length projection onto the trace (<c>ItemPreviewTicker._hostTraceOffset</c>) — on
        ///     whichever side <paramref name="forward" /> selects. Exists so a caller anchoring a loop on the
        ///     balloon's own boundary (<see cref="AppendApproachLoop" />) never has to assume the crossing
        ///     sits exactly one radius along the trace from that projection: with a perpendicular offset
        ///     <c>d</c> the true half-chord is <c>sqrt(r² - d²)</c>, not <c>r</c>, and a bent trace can cross
        ///     the circle's boundary on a segment other than the one the projection itself falls on — both of
        ///     which this walk solves for directly (a real per-segment line/circle intersection) rather than
        ///     approximating.
        /// </summary>
        /// <param name="forward">
        ///     True to walk toward increasing trace offset, false toward decreasing — the caller picks
        ///     whichever side the approach is actually heading, normally by comparing the stroke's own entry
        ///     offset against <paramref name="hostOffset" />, so a figure whose entry lies further along
        ///     travel (Shield, out at the wall) leaves by the forward crossing and one whose entry lies
        ///     behind the host (Bomb's circle, which the sight reaches before the host centre) leaves by the
        ///     backward one, with no special-casing needed between the two.
        /// </param>
        /// <returns>
        ///     False — leaving <paramref name="point" /> and <paramref name="offset" /> at their defaults —
        ///     when there is nothing to walk (fewer than two trace points), the circle can't be reached at
        ///     all (a non-positive radius, or a host projection further than <paramref name="radius" /> from
        ///     every point on the trace), or the trace runs out before ever crossing back out of the circle
        ///     on the requested side (its own end sits inside). A caller degrades to its pre-crossing
        ///     fallback in every one of these, exactly as <see cref="TryFindEntryOffset" />'s own callers do.
        /// </returns>
        internal static bool TryFindCircleCrossing(
            IReadOnlyList<Vector3> tracePoints,
            IReadOnlyList<float> traceArcTable,
            Vector3 center,
            float radius,
            float hostOffset,
            bool forward,
            out Vector3 point,
            out float offset)
        {
            point = default;
            offset = 0f;

            if (tracePoints == null || traceArcTable == null || tracePoints.Count < 2 || radius <= 0f)
            {
                return false;
            }

            var traceLength = traceArcTable[tracePoints.Count - 1];
            var clampedHostOffset = Mathf.Clamp(hostOffset, 0f, traceLength);

            if (!TryLocateSegment(traceArcTable, clampedHostOffset, out var segIndex, out var segT))
            {
                return false;
            }

            return forward
                ? TryWalkCircleForward(tracePoints, traceArcTable, center, radius, segIndex, segT, out point, out offset)
                : TryWalkCircleBackward(tracePoints, traceArcTable, center, radius, segIndex, segT, out point, out offset);
        }

        // Finds which segment of a cumulative-arc table a given offset falls on, and how far into it —
        // shared groundwork for both TryFindCircleCrossing walks below, mirroring how ItemPreviewTicker's
        // own SampleStroke/SampleTrace locate a distance within a table, just returning the segment/local-t
        // pair instead of a sampled point.
        private static bool TryLocateSegment(
            IReadOnlyList<float> arcTable, float offset, out int segIndex, out float segT)
        {
            segIndex = 0;
            segT = 0f;

            var segmentCount = arcTable.Count - 1;
            if (segmentCount < 1)
            {
                return false;
            }

            for (var i = 0; i < segmentCount; i++)
            {
                if (offset > arcTable[i + 1] && i < segmentCount - 1)
                {
                    continue;
                }

                segIndex = i;
                var segmentLength = arcTable[i + 1] - arcTable[i];
                segT = segmentLength <= 1e-6f ? 0f : Mathf.Clamp01((offset - arcTable[i]) / segmentLength);
                return true;
            }

            return false;
        }

        // Walks segIndex..last, seeking the SMALLEST valid crossing t on each — the one nearest the walk's
        // own start on that segment, since forward always enters a segment at its near (low-t) end once
        // past the starting one.
        private static bool TryWalkCircleForward(
            IReadOnlyList<Vector3> tracePoints,
            IReadOnlyList<float> traceArcTable,
            Vector3 center,
            float radius,
            int startSegment,
            float startT,
            out Vector3 point,
            out float offset)
        {
            point = default;
            offset = 0f;

            for (var i = startSegment; i < tracePoints.Count - 1; i++)
            {
                var a = tracePoints[i];
                var b = tracePoints[i + 1];
                var tMin = i == startSegment ? startT : 0f;

                if (!TryGetSegmentCircleRoots(a, b, center, radius, out var t1, out var t2))
                {
                    continue;
                }

                if (!TryPickInRange(t1, t2, tMin, 1f, preferLarger: false, out var t))
                {
                    continue;
                }

                offset = traceArcTable[i] + (t * (traceArcTable[i + 1] - traceArcTable[i]));
                point = Vector3.Lerp(a, b, t);
                return true;
            }

            return false;
        }

        // Mirrors TryWalkCircleForward, walking startSegment..0 and seeking the LARGEST valid crossing t on
        // each — backward always enters a segment at its far (high-t) end once past the starting one.
        private static bool TryWalkCircleBackward(
            IReadOnlyList<Vector3> tracePoints,
            IReadOnlyList<float> traceArcTable,
            Vector3 center,
            float radius,
            int startSegment,
            float startT,
            out Vector3 point,
            out float offset)
        {
            point = default;
            offset = 0f;

            for (var i = startSegment; i >= 0; i--)
            {
                var a = tracePoints[i];
                var b = tracePoints[i + 1];
                var tMax = i == startSegment ? startT : 1f;

                if (!TryGetSegmentCircleRoots(a, b, center, radius, out var t1, out var t2))
                {
                    continue;
                }

                if (!TryPickInRange(t1, t2, 0f, tMax, preferLarger: true, out var t))
                {
                    continue;
                }

                offset = traceArcTable[i] + (t * (traceArcTable[i + 1] - traceArcTable[i]));
                point = Vector3.Lerp(a, b, t);
                return true;
            }

            return false;
        }

        // Standard segment/circle intersection: the two parameters along A->B (t1 <= t2, unclamped to
        // [0, 1]) where the point sits exactly on the circle. False for a degenerate (zero-length) segment
        // or a line that never reaches the circle at all — callers treat either the same as "no root here."
        private static bool TryGetSegmentCircleRoots(
            Vector3 a, Vector3 b, Vector3 center, float radius, out float t1, out float t2)
        {
            var dx = b.x - a.x;
            var dy = b.y - a.y;
            var fx = a.x - center.x;
            var fy = a.y - center.y;

            var qa = (dx * dx) + (dy * dy);
            if (qa <= 1e-10f)
            {
                t1 = 0f;
                t2 = 0f;
                return false;
            }

            var qb = 2f * ((fx * dx) + (fy * dy));
            var qc = (fx * fx) + (fy * fy) - (radius * radius);

            var discriminant = (qb * qb) - (4f * qa * qc);
            if (discriminant < 0f)
            {
                t1 = 0f;
                t2 = 0f;
                return false;
            }

            var sqrtDiscriminant = Mathf.Sqrt(discriminant);
            t1 = (-qb - sqrtDiscriminant) / (2f * qa);
            t2 = (-qb + sqrtDiscriminant) / (2f * qa);
            return true;
        }

        // t1 <= t2 always (TryGetSegmentCircleRoots' own construction), so "smallest in range" is just t1
        // if it qualifies, else t2, and "largest" is the mirror — a shared picker so the two walks above
        // don't each carry their own copy of this.
        private static bool TryPickInRange(float t1, float t2, float lo, float hi, bool preferLarger, out float picked)
        {
            var firstChoice = preferLarger ? t2 : t1;
            var secondChoice = preferLarger ? t1 : t2;

            if (firstChoice >= lo && firstChoice <= hi)
            {
                picked = firstChoice;
                return true;
            }

            if (secondChoice >= lo && secondChoice <= hi)
            {
                picked = secondChoice;
                return true;
            }

            picked = 0f;
            return false;
        }

        // One trace segment against every segment of the stroke (including the closed wrap leg), kept
        // outside the outer loop so each level of nesting stays shallow enough to read at a glance.
        private static bool TryFindNearestCrossing(
            Vector3 p1,
            Vector3 p2,
            in ItemPreviewStroke stroke,
            IReadOnlyList<Vector3> points,
            int segmentCount,
            out int bestSegment,
            out float bestTStroke,
            out float bestTTrace)
        {
            var rx = p2.x - p1.x;
            var ry = p2.y - p1.y;

            bestTTrace = float.MaxValue;
            bestSegment = -1;
            bestTStroke = 0f;

            for (var j = 0; j < segmentCount; j++)
            {
                GetStrokeSegment(stroke, j, points, out var a, out var b);
                var sx = b.x - a.x;
                var sy = b.y - a.y;

                var denom = (rx * sy) - (ry * sx);
                if (Mathf.Abs(denom) < ParallelEpsilon)
                {
                    continue;
                }

                var dx = a.x - p1.x;
                var dy = a.y - p1.y;

                var tTrace = ((dx * sy) - (dy * sx)) / denom;

                // A later (or equal) crossing along this trace segment can't beat one already held,
                // regardless of whether it even lands on the stroke segment — skip before touching
                // tStroke at all.
                if (tTrace < 0f || tTrace > 1f || tTrace >= bestTTrace)
                {
                    continue;
                }

                var tStroke = ((dx * ry) - (dy * rx)) / denom;
                if (tStroke < 0f || tStroke > 1f)
                {
                    continue;
                }

                bestTTrace = tTrace;
                bestSegment = j;
                bestTStroke = tStroke;
            }

            return bestSegment >= 0;
        }

        // The wrap leg (closed strokes only) runs from the last point back to the first — every other
        // segment is the ordinary i -> i + 1 pairing.
        private static void GetStrokeSegment(
            in ItemPreviewStroke stroke, int segmentIndex, IReadOnlyList<Vector3> points, out Vector3 a, out Vector3 b)
        {
            if (segmentIndex < stroke.Count - 1)
            {
                a = points[stroke.Start + segmentIndex];
                b = points[stroke.Start + segmentIndex + 1];
            }
            else
            {
                a = points[stroke.Start + stroke.Count - 1];
                b = points[stroke.Start];
            }
        }

        // Converts a winning (segment, tStroke) pair into an arc length along the whole stroke.
        private static float ComputeArcOffset(
            in ItemPreviewStroke stroke,
            IReadOnlyList<Vector3> points,
            IReadOnlyList<float> arcTable,
            int segment,
            float tStroke)
        {
            var baseIndex = segment < stroke.Count - 1 ? stroke.Start + segment : stroke.Start + stroke.Count - 1;
            GetStrokeSegment(stroke, segment, points, out var segFrom, out var segTo);

            var segDx = segTo.x - segFrom.x;
            var segDy = segTo.y - segFrom.y;
            var segLength = Mathf.Sqrt((segDx * segDx) + (segDy * segDy));

            return arcTable[baseIndex] + (tStroke * segLength);
        }
    }
}
