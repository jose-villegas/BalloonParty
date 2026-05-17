using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared.Animation
{
    /// <summary>
    ///     Generic path interpolation utilities. Stateless, allocation-minimal.
    /// </summary>
    internal static class PathHelper
    {
        /// <summary>
        ///     Builds a smooth Catmull-Rom subdivided path through the first
        ///     <paramref name="pointCount" /> entries of <paramref name="waypoints" />.
        /// </summary>
        internal static Vector3[] CatmullRomPath(
            List<Vector3> waypoints,
            int pointCount,
            int subdivisions = 6)
        {
            var count = Mathf.Min(pointCount, waypoints.Count);

            if (count <= 1)
            {
                return count == 1 ? new[] { waypoints[0] } : Array.Empty<Vector3>();
            }

            var totalPoints = ((count - 1) * subdivisions) + 1;
            var result = new Vector3[totalPoints];
            var idx = 0;

            for (var i = 0; i < count - 1; i++)
            {
                var p0 = waypoints[Mathf.Max(i - 1, 0)];
                var p1 = waypoints[i];
                var p2 = waypoints[i + 1];
                var p3 = waypoints[Mathf.Min(i + 2, count - 1)];

                for (var s = 0; s < subdivisions; s++)
                {
                    var t = s / (float)subdivisions;
                    result[idx++] = CatmullRom(p0, p1, p2, p3, t);
                }
            }

            result[idx] = waypoints[count - 1];
            return result;
        }

        /// <summary>
        ///     Builds a smooth closed-loop Catmull-Rom path through the first
        ///     <paramref name="pointCount" /> entries of <paramref name="waypoints" />.
        ///     The last point connects back to the first.
        /// </summary>
        internal static Vector3[] CatmullRomLoop(
            List<Vector3> waypoints,
            int pointCount,
            int subdivisions = 6)
        {
            var count = Mathf.Min(pointCount, waypoints.Count);

            if (count <= 1)
            {
                return count == 1 ? new[] { waypoints[0] } : Array.Empty<Vector3>();
            }

            // +1 segment for the closing edge back to start
            var totalPoints = (count * subdivisions) + 1;
            var result = new Vector3[totalPoints];
            var idx = 0;

            for (var i = 0; i < count; i++)
            {
                var p0 = waypoints[((i - 1) + count) % count];
                var p1 = waypoints[i];
                var p2 = waypoints[(i + 1) % count];
                var p3 = waypoints[(i + 2) % count];

                for (var s = 0; s < subdivisions; s++)
                {
                    var t = s / (float)subdivisions;
                    result[idx++] = CatmullRom(p0, p1, p2, p3, t);
                }
            }

            result[idx] = waypoints[0];
            return result;
        }

        /// <summary>
        ///     Evaluates a single point on a Catmull-Rom spline defined by four
        ///     control points at parameter <paramref name="t" /> (0–1).
        /// </summary>
        internal static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            var tt = t * t;
            var ttt = tt * t;

            return 0.5f * (
                (2f * p1) +
                ((-p0 + p2) * t) +
                (((2f * p0) - (5f * p1) + (4f * p2) - p3) * tt) +
                ((-p0 + (3f * p1) - (3f * p2) + p3) * ttt));
        }

        /// <summary>
        ///     Samples a <see cref="Vector3" /> array at a fractional index,
        ///     linearly interpolating between the two nearest entries.
        /// </summary>
        internal static Vector3 SampleAt(Vector3[] array, float index)
        {
            var maxIdx = array.Length - 1;
            index = Mathf.Clamp(index, 0f, maxIdx);
            var lo = Mathf.FloorToInt(index);
            var hi = Mathf.Min(lo + 1, maxIdx);
            return Vector3.Lerp(array[lo], array[hi], index - lo);
        }

        /// <summary>
        ///     Samples a <see langword="float" /> array at a fractional index,
        ///     linearly interpolating between the two nearest entries.
        /// </summary>
        internal static float SampleAt(float[] array, float index)
        {
            var maxIdx = array.Length - 1;
            index = Mathf.Clamp(index, 0f, maxIdx);
            var lo = Mathf.FloorToInt(index);
            var hi = Mathf.Min(lo + 1, maxIdx);
            return Mathf.Lerp(array[lo], array[hi], index - lo);
        }

        /// <summary>
        ///     Re-samples a sparse <see langword="float" /> array into
        ///     <paramref name="sampleCount" /> evenly-spaced values via linear interpolation.
        /// </summary>
        internal static float[] ResampleLinear(float[] values, int sampleCount)
        {
            var result = new float[sampleCount];
            var lastSrc = values.Length - 1;

            for (var i = 0; i < sampleCount; i++)
            {
                var t = (float)i / (sampleCount - 1) * lastSrc;
                var lo = Mathf.FloorToInt(t);
                var hi = Mathf.Min(lo + 1, lastSrc);
                result[i] = Mathf.Lerp(values[lo], values[hi], t - lo);
            }

            return result;
        }

        /// <summary>
        ///     Builds a cumulative prefix-sum array from <paramref name="sizes" />.
        ///     Result has length <c>sizes.Length + 1</c> with <c>result[0] == 0</c>.
        /// </summary>
        internal static int[] PrefixSum(int[] sizes)
        {
            var result = new int[sizes.Length + 1];
            for (var i = 0; i < sizes.Length; i++)
            {
                result[i + 1] = result[i] + sizes[i];
            }

            return result;
        }

        /// <summary>
        ///     Fills <paramref name="count" /> points between <paramref name="start" />
        ///     and <paramref name="end" /> using recursive midpoint displacement.
        ///     Produces fractal jagged lines typical of electricity arcs.
        ///     <paramref name="displacement" /> is the initial perpendicular offset
        ///     magnitude; <paramref name="decay" /> (0–1) scales it each recursion level
        ///     (0.5 = classic lightning, higher = more fine detail).
        /// </summary>
        internal static void MidpointDisplacement(
            Vector3 start,
            Vector3 end,
            float displacement,
            float decay,
            Vector3[] buffer,
            int offset,
            int count)
        {
            buffer[offset] = start;
            buffer[offset + count - 1] = end;

            if (count <= 2)
            {
                return;
            }

            var dir = (end - start).normalized;
            var perp = new Vector3(-dir.y, dir.x, 0f);

            Subdivide(buffer, offset, 0, count - 1, displacement, decay, perp);
        }

        private static void Subdivide(
            Vector3[] buffer,
            int offset,
            int left,
            int right,
            float scale,
            float decay,
            Vector3 perp)
        {
            if (right - left <= 1)
            {
                return;
            }

            var mid = (left + right) / 2;
            var midpoint = (buffer[offset + left] + buffer[offset + right]) * 0.5f;
            buffer[offset + mid] = midpoint + perp * UnityEngine.Random.Range(-scale, scale);

            var reduced = scale * decay;
            Subdivide(buffer, offset, left, mid, reduced, decay, perp);
            Subdivide(buffer, offset, mid, right, reduced, decay, perp);
        }
    }
}

