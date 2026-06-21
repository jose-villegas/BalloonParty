using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>
    ///     Stateless geometry helpers as extension methods: 2D proximity tests on <see cref="Vector2" />
    ///     and point-set operations on a <see cref="Vector3" /> list.
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
    }
}
