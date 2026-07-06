using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     The paint splash's target region: an isosceles triangle whose median runs along the
    ///     projectile's travel direction. The apex sits at the hit point shifted by <c>Offset</c> along
    ///     the axis; the triangle reaches <c>Length</c> further and fans out to <c>BaseWidth</c> at its
    ///     far edge. The region is circle-packed with blobs; balloons within a blob's radius get painted
    ///     (see <see cref="PaintItemHandler" />). All coordinates are world-space XY (z ignored).
    /// </summary>
    internal readonly struct PaintTriangle
    {
        private const float DirectionEpsilon = 1e-6f;

        public readonly Vector2 Apex;
        public readonly Vector2 Left;
        public readonly Vector2 Right;
        public readonly Vector2 Axis;
        public readonly float Length;
        public readonly float BaseWidth;

        private PaintTriangle(Vector2 apex, Vector2 left, Vector2 right, Vector2 axis, float length, float baseWidth)
        {
            Apex = apex;
            Left = left;
            Right = right;
            Axis = axis;
            Length = length;
            BaseWidth = baseWidth;
        }

        // No direction (e.g. tests) defaults to up so the shape doesn't collapse to a line. Negative
        // SpreadLength opens the triangle backward, positive forward; Offset always shifts along travel.
        public static PaintTriangle Build(Vector2 hit, Vector2 direction, PaintSettings paint)
        {
            var travel = direction.sqrMagnitude < DirectionEpsilon ? Vector2.up : direction.normalized;
            var toBase = paint.SpreadLength >= 0f ? travel : -travel;
            var length = Mathf.Abs(paint.SpreadLength);
            var baseWidth = Mathf.Max(paint.SpreadBaseWidth, 0f);
            var perp = Vector2.Perpendicular(toBase);

            var apex = hit + (travel * paint.SpreadOffset);
            var baseCenter = apex + (toBase * length);
            var halfWidth = baseWidth * 0.5f;

            return new PaintTriangle(apex, baseCenter - (perp * halfWidth), baseCenter + (perp * halfWidth),
                toBase, length, baseWidth);
        }

        // Hex-packs circles of radius blobRadius into the triangle (rows step r*sqrt(3), staggered by r,
        // mirrored about the median). Capped at maxBlobs; always yields at least one (centroid).
        public void PackBlobs(float blobRadius, int maxBlobs, List<Vector2> results)
        {
            results.Clear();
            var r = Mathf.Max(blobRadius, 1e-3f);
            var perp = Vector2.Perpendicular(Axis);
            var rowSpacing = r * Mathf.Sqrt(3f);
            var rowIndex = 0;

            for (var s = r; s <= Length && results.Count < maxBlobs; s += rowSpacing, rowIndex++)
            {
                var usable = (BaseWidth * 0.5f * (s / Length)) - r;
                if (usable < 0f)
                {
                    continue;
                }

                var rowCentre = Apex + (Axis * s);
                var start = (rowIndex & 1) == 0 ? 0f : r;

                for (var t = start; t <= usable + 1e-4f && results.Count < maxBlobs; t += 2f * r)
                {
                    results.Add(rowCentre + (perp * t));
                    if (t > 1e-4f && results.Count < maxBlobs)
                    {
                        results.Add(rowCentre - (perp * t));
                    }
                }
            }

            if (results.Count == 0)
            {
                results.Add((Apex + Left + Right) / 3f);
            }
        }
    }
}
