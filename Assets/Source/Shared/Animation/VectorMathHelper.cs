using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Animation
{
    /// <summary>
    ///     Stateless geometry utilities for common point-set operations.
    /// </summary>
    internal static class VectorMathHelper
    {
        /// <summary>
        ///     Returns the centroid (arithmetic mean) of the first
        ///     <paramref name="count" /> entries in <paramref name="points" />.
        /// </summary>
        internal static Vector3 Centroid(List<Vector3> points, int count)
        {
            var sum = Vector3.zero;
            for (var i = 0; i < count; i++)
            {
                sum += points[i];
            }

            return sum / count;
        }

        /// <summary>
        ///     Returns the maximum distance from <paramref name="center" /> to any of
        ///     the first <paramref name="count" /> entries in <paramref name="points" />.
        /// </summary>
        internal static float BoundingRadius(List<Vector3> points, int count, Vector3 center)
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

