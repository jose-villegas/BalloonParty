using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>
    ///     Stateless geometry helpers as extension methods: 2D proximity tests on <see cref="Vector2" />,
    ///     point-set operations on a <see cref="Vector3" /> list, and a 1D framing clamp for fitting a
    ///     span inside a view window.
    /// </summary>
    internal static class VectorMathExtensions
    {
        public static float SqrDistance2D(this Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return (dx * dx) + (dy * dy);
        }

        // Vector3 overloads for world-space callers; the z component is ignored (this is a 2D test).
        public static float SqrDistance2D(this Vector3 a, Vector3 b)
        {
            return ((Vector2)a).SqrDistance2D(b);
        }

        /// <summary>
        ///     Unit direction in the XY plane for <paramref name="radians" />. Multiply by a radius
        ///     and add to a centre to place points on a circle. Returns <see cref="Vector2" />; for
        ///     world-space callers assign it to a <c>Vector3</c> local first (a direct
        ///     <c>Vector3 + Vector2</c> is ambiguous in Unity).
        /// </summary>
        public static Vector2 DirectionFromAngle(float radians)
        {
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        public static bool WithinRadius(this Vector2 a, Vector2 b, float radius)
        {
            return a.SqrDistance2D(b) <= radius * radius;
        }

        public static bool WithinRadius(this Vector3 a, Vector3 b, float radius)
        {
            return ((Vector2)a).WithinRadius(b, radius);
        }

        /// <summary>
        ///     The centroid (arithmetic mean) of the first <paramref name="count" /> entries.
        /// </summary>
        public static Vector3 Centroid(this IReadOnlyList<Vector3> points, int count)
        {
            var sum = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                sum += points[i];
            }

            return sum / count;
        }

        /// <summary>
        ///     The maximum distance from <paramref name="center" /> to any of the first
        ///     <paramref name="count" /> entries.
        /// </summary>
        public static float BoundingRadius(this IReadOnlyList<Vector3> points, int count, Vector3 center)
        {
            var maxDist = 0f;
            for (var i = 0; i < count; i++)
            {
                var d = Vector3.Distance(center, points[i]);
                if (d > maxDist)
                {
                    maxDist = d;
                }
            }

            return maxDist;
        }

        /// <summary>
        ///     The axis-aligned bounding box of the first <paramref name="count" /> entries.
        ///     <paramref name="count" /> must be at least 1.
        /// </summary>
        public static Bounds Bounds(this IReadOnlyList<Vector3> points, int count)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return bounds;
        }

        /// <summary>
        ///     Clamps <paramref name="value" /> so the span <c>[min,max]</c> stays inside a window of
        ///     half-width <paramref name="halfExtent" /> (shrunk by <paramref name="padding" />) centred on
        ///     it — the move that keeps a tracked region within the camera frustum. If the span is wider
        ///     than the window the clamp bounds would cross, so it falls back to <paramref name="fallback" />.
        /// </summary>
        public static float ClampToWindow(float value, float min, float max, float halfExtent, float padding, float fallback)
        {
            var lo = max - halfExtent + padding;
            var hi = min + halfExtent - padding;
            return lo > hi ? fallback : Mathf.Clamp(value, lo, hi);
        }
    }
}
