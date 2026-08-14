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
