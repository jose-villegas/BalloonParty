using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    /// <summary>Stateless geometry helpers: 2D distance/radius tests, point-set ops, a framing clamp.</summary>
    internal static class VectorMathExtensions
    {
        public static float SqrDistance2D(this Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return (dx * dx) + (dy * dy);
        }

        // Ignores z — 2D test only.
        public static float SqrDistance2D(this Vector3 a, Vector3 b)
        {
            return ((Vector2)a).SqrDistance2D(b);
        }

        /// <summary>Unit direction in the XY plane for <paramref name="radians" />; assign to a <c>Vector3</c> local before adding (direct <c>Vector3 + Vector2</c> is ambiguous).</summary>
        public static Vector2 DirectionFromAngle(float radians)
        {
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        /// <summary>The direction's angle mapped once around the full circle to <c>[0,1)</c>: 0 = +x (east),
        /// increasing counter-clockwise (0.25 = +y/up). Inverse of <see cref="DirectionFromAngle" />; useful
        /// for indexing a full-circle gradient by direction. A zero vector returns 0.</summary>
        public static float Angle01(this Vector2 direction)
        {
            if (direction.sqrMagnitude < 1e-8f)
            {
                return 0f;
            }

            var t = Mathf.Atan2(direction.y, direction.x) / (2f * Mathf.PI);
            return t < 0f ? t + 1f : t;
        }

        public static bool WithinRadius(this Vector2 a, Vector2 b, float radius)
        {
            return a.SqrDistance2D(b) <= radius * radius;
        }

        public static bool WithinRadius(this Vector3 a, Vector3 b, float radius)
        {
            return ((Vector2)a).WithinRadius(b, radius);
        }

        /// <summary>Centroid of the first <paramref name="count" /> entries.</summary>
        public static Vector3 Centroid(this IReadOnlyList<Vector3> points, int count)
        {
            var sum = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                sum += points[i];
            }

            return sum / count;
        }

        /// <summary>Max distance from <paramref name="center" /> to any of the first <paramref name="count" /> entries.</summary>
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

        /// <summary>Axis-aligned bounding box of the first <paramref name="count" /> entries (must be at least 1).</summary>
        public static Bounds Bounds(this IReadOnlyList<Vector3> points, int count)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            for (var i = 1; i < count; i++)
            {
                bounds.Encapsulate(points[i]);
            }

            return bounds;
        }

        /// <summary>Clamps <paramref name="value" /> so <c>[min,max]</c> stays inside a window of half-width <paramref name="halfExtent" />; falls back to <paramref name="fallback" /> if the span is too wide to fit.</summary>
        public static float ClampToWindow(float value, float min, float max, float halfExtent, float padding, float fallback)
        {
            var lo = max - halfExtent + padding;
            var hi = min + halfExtent - padding;
            return lo > hi ? fallback : Mathf.Clamp(value, lo, hi);
        }
    }
}
