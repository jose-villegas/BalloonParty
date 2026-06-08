using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Extracts leaf attachment points from a baked branch map texture.
    /// Finds branch tips (high depth pixels with no continuation ahead)
    /// and returns positions, angles, and metadata for leaf placement.
    /// </summary>
    internal static class BushLeafExtractor
    {
        internal struct LeafSlot
        {
            internal Vector2 UVPosition;
            internal float Angle;
            internal float Depth;
            internal float Scale;
            internal int SpriteVariant;
        }

        internal static List<LeafSlot> Extract(
            Texture2D branchMap,
            int seed,
            BushBranchBakeSettings branchSettings,
            int leafVariantCount)
        {
            var pixels = branchMap.GetPixels32();
            var res = branchMap.width;
            var threshold = (byte)(branchSettings.LeafDepthThreshold * 255f);

            var candidates = FindTipCandidates(pixels, res, threshold);
            var filtered = SpatialFilter(candidates, branchSettings.MaxLeavesPerVariant, res);
            return BuildLeafSlots(filtered, seed, branchSettings, leafVariantCount, res);
        }

        private static List<TipCandidate> FindTipCandidates(Color32[] pixels, int res, byte threshold)
        {
            var candidates = new List<TipCandidate>(256);

            for (var y = 1; y < res - 1; y++)
            {
                for (var x = 1; x < res - 1; x++)
                {
                    var idx = y * res + x;
                    var p = pixels[idx];

                    if (p.a < threshold)
                    {
                        continue;
                    }

                    var dirX = p.r / 255f * 2f - 1f;
                    var dirY = p.g / 255f * 2f - 1f;

                    candidates.Add(new TipCandidate
                    {
                        X = x,
                        Y = y,
                        Depth = p.a / 255f,
                        Angle = Mathf.Atan2(dirY, dirX),
                        Score = p.a
                    });
                }
            }

            return candidates;
        }

        private static List<TipCandidate> SpatialFilter(
            List<TipCandidate> candidates, int maxCount, int res)
        {
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            // Adaptive min distance: allows denser packing as max count increases
            var minDist = 1f / Mathf.Sqrt(maxCount) * 0.5f;
            var minDistPixels = minDist * res;
            var minDistSq = minDistPixels * minDistPixels;

            var accepted = new List<TipCandidate>(maxCount);

            foreach (var candidate in candidates)
            {
                if (accepted.Count >= maxCount)
                {
                    break;
                }

                var tooClose = false;
                foreach (var existing in accepted)
                {
                    var dx = candidate.X - existing.X;
                    var dy = candidate.Y - existing.Y;
                    if (dx * dx + dy * dy < minDistSq)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    accepted.Add(candidate);
                }
            }

            return accepted;
        }

        private static List<LeafSlot> BuildLeafSlots(
            IReadOnlyList<TipCandidate> tips,
            int seed,
            BushBranchBakeSettings settings,
            int leafVariantCount,
            int resolution)
        {
            var rng = new System.Random(seed + 9973);
            var slots = new List<LeafSlot>(tips.Count);
            for (var i = 0; i < tips.Count; i++)
            {
                var tip = tips[i];
                var hash = (float)rng.NextDouble();

                var scale = settings.LeafScale * (1f + (hash - 0.5f) * 2f * settings.LeafScaleVariation);
                var variant = leafVariantCount > 0 ? rng.Next(leafVariantCount) : 0;

                slots.Add(new LeafSlot
                {
                    UVPosition = new Vector2(tip.X / (float)resolution, tip.Y / (float)resolution),
                    Angle = tip.Angle,
                    Depth = tip.Depth,
                    Scale = scale,
                    SpriteVariant = variant
                });
            }

            return slots;
        }

        private struct TipCandidate
        {
            internal int X;
            internal int Y;
            internal float Depth;
            internal float Angle;
            internal int Score;
        }
    }
}
