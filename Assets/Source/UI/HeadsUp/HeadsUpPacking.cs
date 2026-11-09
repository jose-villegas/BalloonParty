using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     Where a new heads-up line goes so it doesn't sit on the lines still showing: the nearest spot to
    ///     where it happened, stepping out in rings of one line height up and down and half a line width
    ///     sideways, kept inside the camera's view. When the board is too crowded for a clean spot it takes the one
    ///     that overlaps least, so a burst still reads instead of stacking into one smudge.
    /// </summary>
    internal static class HeadsUpPacking
    {
        /// <summary>Overlap a spot may have, as a share of the new line's area, and still count as free.</summary>
        internal const float AllowedOverlap = 0.15f;

        private const int Rings = 4;

        // Ring by ring, each ring's edge in steps of (half a line width, one line height), nearest rings first.
        private static readonly Vector2Int[] RingSteps = BuildRingSteps();

        /// <summary>What an orthographic <paramref name="camera" /> shows, in world space.</summary>
        internal static Rect ViewRect(Camera camera)
        {
            var halfHeight = camera.orthographicSize;
            var halfSize = new Vector2(halfHeight * camera.aspect, halfHeight);
            return new Rect((Vector2)camera.transform.position - halfSize, halfSize * 2f);
        }

        /// <summary>
        ///     The centre for a line of <paramref name="size" /> wanted at <paramref name="wanted" />, inside
        ///     <paramref name="bounds" />, clear of <paramref name="occupied" /> where it can be.
        /// </summary>
        internal static Vector2 Place(Rect bounds, Vector2 wanted, Vector2 size, IReadOnlyList<Rect> occupied)
        {
            var halfSize = size * 0.5f;
            var best = RectAnchorMath.ClampInside(bounds, wanted, halfSize);
            var bestOverlap = WorstOverlap(best, size, occupied);
            var bestDistance = 0f;
            var ring = 0;
            foreach (var step in RingSteps)
            {
                // A free spot on an inner ring beats anything farther out.
                var stepRing = Mathf.Max(Mathf.Abs(step.x), Mathf.Abs(step.y));
                if (stepRing != ring && bestOverlap <= AllowedOverlap)
                {
                    return best;
                }

                ring = stepRing;
                var candidate = RectAnchorMath.ClampInside(
                    bounds, wanted + new Vector2(step.x * halfSize.x, step.y * size.y), halfSize);
                var overlap = WorstOverlap(candidate, size, occupied);
                var distance = (candidate - wanted).sqrMagnitude;
                if (IsBetter(overlap, distance, bestOverlap, bestDistance))
                {
                    best = candidate;
                    bestOverlap = overlap;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>The largest share of a line at <paramref name="center" /> that any occupied rect covers.</summary>
        internal static float WorstOverlap(Vector2 center, Vector2 size, IReadOnlyList<Rect> occupied)
        {
            var area = size.x * size.y;
            if (area <= 0f || occupied == null)
            {
                return 0f;
            }

            var rect = new Rect(center - size * 0.5f, size);
            var worst = 0f;
            for (var i = 0; i < occupied.Count; i++)
            {
                var other = occupied[i];
                var width = Mathf.Min(rect.xMax, other.xMax) - Mathf.Max(rect.xMin, other.xMin);
                var height = Mathf.Min(rect.yMax, other.yMax) - Mathf.Max(rect.yMin, other.yMin);
                if (width > 0f && height > 0f)
                {
                    worst = Mathf.Max(worst, width * height / area);
                }
            }

            return worst;
        }

        private static Vector2Int[] BuildRingSteps()
        {
            var steps = new List<Vector2Int>();
            for (var ring = 1; ring <= Rings; ring++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    for (var dy = -ring; dy <= ring; dy++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) == ring)
                        {
                            steps.Add(new Vector2Int(dx, dy));
                        }
                    }
                }
            }

            return steps.ToArray();
        }

        // A free spot beats a crowded one; between free spots the nearer wins, between crowded ones the less covered.
        private static bool IsBetter(float overlap, float distance, float bestOverlap, float bestDistance)
        {
            var free = overlap <= AllowedOverlap;
            var bestFree = bestOverlap <= AllowedOverlap;
            if (free != bestFree)
            {
                return free;
            }

            return free ? distance < bestDistance : overlap < bestOverlap;
        }
    }
}
