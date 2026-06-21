using System.Collections.Generic;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Pure geometry for the chain-lightning effect: the jagged per-renderer bolt buffers and the
    ///     smoothed glow path through the per-jump centroids. Stateless, so it's testable and reusable
    ///     (the editor preview drives it) without a live <see cref="ChainLightningView" />.
    /// </summary>
    internal static class ChainLightningGeometry
    {
        /// <summary>
        ///     Pre-computes jagged bolt segments for all jumps and renderers.
        ///     Returns per-renderer position buffers and cumulative offset array.
        /// </summary>
        internal static (Vector3[][] lineBuffers, int[] cumOffsets) BuildBoltBuffers(
            IReadOnlyList<Vector3> positions,
            int rendererCount,
            float segmentsMultiplier,
            float randomness,
            float fractalDecay)
        {
            var jumpCount = positions.Count - 1;

            var segmentSizes = new int[jumpCount];
            for (var i = 0; i < jumpCount; i++)
            {
                var d = Vector3.Distance(positions[i], positions[i + 1]);
                segmentSizes[i] = Mathf.Max(Mathf.FloorToInt(d * segmentsMultiplier), 2);
            }

            var cumOffsets = PathHelper.PrefixSum(segmentSizes);
            var totalPoints = cumOffsets[jumpCount];

            var lineBuffers = new Vector3[rendererCount][];
            for (var j = 0; j < rendererCount; j++)
            {
                lineBuffers[j] = new Vector3[totalPoints];
                for (var i = 0; i < jumpCount; i++)
                {
                    FillSegment(
                        positions[i],
                        positions[i + 1],
                        segmentSizes[i],
                        randomness,
                        fractalDecay,
                        lineBuffers[j],
                        cumOffsets[i]);
                }
            }

            return (lineBuffers, cumOffsets);
        }

        /// <summary>
        ///     Builds a smooth Catmull-Rom path through the per-jump centroids so the
        ///     glow sprite can slide instead of snapping between discrete positions.
        ///     Also returns interpolated diameters that match each path sample.
        /// </summary>
        internal static (Vector3[] positions, float[] diameters) BuildGlowPath(
            IReadOnlyList<Vector3> targetPositions,
            int subdivisions)
        {
            var (centroids, rawDiameters) = ComputeStageCentroids(targetPositions);

            if (centroids.Count <= 1)
            {
                return (centroids.ToArray(), rawDiameters);
            }

            var smoothPositions = PathHelper.CatmullRomPath(centroids, centroids.Count, subdivisions);
            var smoothDiameters = PathHelper.ResampleLinear(rawDiameters, smoothPositions.Length);

            return (smoothPositions, smoothDiameters);
        }

        private static void FillSegment(
            Vector3 start,
            Vector3 end,
            int segments,
            float displacement,
            float fractalDecay,
            Vector3[] buffer,
            int offset)
        {
            PathHelper.MidpointDisplacement(start, end, displacement, fractalDecay, buffer, offset, segments);
        }

        /// <summary>
        ///     Computes the centroid and bounding diameter for each visible glow stage.
        ///     Stage <c>s</c> (1-indexed) covers <c>targetPositions[0..s]</c>.
        /// </summary>
        private static (List<Vector3> centroids, float[] diameters) ComputeStageCentroids(
            IReadOnlyList<Vector3> targetPositions)
        {
            var stageCount = targetPositions.Count - 1;
            var centroids = new List<Vector3>(stageCount);
            var diameters = new float[stageCount];

            for (var stage = 1; stage <= stageCount; stage++)
            {
                var count = stage + 1;
                var centroid = targetPositions.Centroid(count);
                centroids.Add(centroid);

                var radius = targetPositions.BoundingRadius(count, centroid);
                diameters[stage - 1] = (radius + 1f) * 2f;
            }

            return (centroids, diameters);
        }
    }
}
