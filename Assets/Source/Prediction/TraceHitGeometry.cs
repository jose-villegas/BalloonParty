using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Prediction
{
    /// <summary>
    ///     Pure polyline-vs-circle math, factored out of <see cref="TraceHitMarker"/> so the segment
    ///     projection/intersection is edit-mode testable without a MonoBehaviour.
    /// </summary>
    internal static class TraceHitGeometry
    {
        /// <summary>
        ///     Finds where the polyline FIRST pierces the circle's surface, walking segments in point
        ///     order — the physical strike point a projectile travelling the trace would touch, not the
        ///     perpendicular-closest point (which sits ~90° off anywhere but a tangential graze).
        ///     Solved per segment as line-circle intersection: from the (unclamped) perpendicular foot,
        ///     the entry lies half a chord back along the travel direction.
        ///     <paramref name="centrality"/> is how central the crossing is: 1 = the line runs through
        ///     the centre (a direct aim), 0 = it only grazes the surface tangentially (one touch point,
        ///     the in==out case) — the natural driver for hit-confidence feedback.
        /// </summary>
        internal static bool TryFindSurfaceHit(
            IReadOnlyList<Vector3> points,
            Vector3 center,
            float radius,
            out Vector3 hitPoint,
            out float centrality)
        {
            hitPoint = default;
            centrality = 0f;

            if (points == null || points.Count < 2 || radius <= 0f)
            {
                return false;
            }

            var radiusSqr = radius * radius;

            for (var i = 0; i < points.Count - 1; i++)
            {
                var a = points[i];
                var segment = points[i + 1] - a;
                var segLength = segment.magnitude;
                if (segLength < Mathf.Epsilon)
                {
                    continue;
                }

                var direction = segment / segLength;
                var tFoot = Vector3.Dot(center - a, direction);
                var foot = a + direction * tFoot;
                var footSqrDistance = (foot - center).sqrMagnitude;
                if (footSqrDistance > radiusSqr)
                {
                    continue;
                }

                var halfChord = Mathf.Sqrt(radiusSqr - footSqrDistance);
                var tEntry = tFoot - halfChord;
                var tExit = tFoot + halfChord;

                // The overlap must actually touch this segment's [0, length] span: an intersection
                // entirely behind the start (tExit < 0) or past the end (tEntry > length) belongs to
                // another segment — walked in point order, so the first accepted hit is the physical
                // first strike. A segment STARTING inside the circle contributes its start point.
                if (tEntry > segLength || tExit < 0f)
                {
                    continue;
                }

                hitPoint = a + direction * Mathf.Max(tEntry, 0f);
                centrality = 1f - Mathf.Sqrt(footSqrDistance) / radius;
                return true;
            }

            return false;
        }
    }
}
