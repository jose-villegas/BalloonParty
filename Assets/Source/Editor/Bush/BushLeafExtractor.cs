using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Extracts leaf attachment points from branch generator terminal segment tips.
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
            int seed,
            BushBranchBakeSettings branchSettings,
            int leafVariantCount)
        {
            var segments = BushBranchGenerator.Generate(seed, branchSettings);
            var tips = FindTerminalTips(segments, branchSettings.LeafDepthThreshold, branchSettings.LeafAttachmentBias);
            var filtered = SpatialFilter(tips, branchSettings.MaxLeavesPerVariant);
            return BuildLeafSlots(filtered, seed, branchSettings, leafVariantCount);
        }

        /// <summary>
        /// Backward-compat overload — ignores the texture and extracts from the generator directly.
        /// </summary>
        internal static List<LeafSlot> Extract(
            Texture2D branchMap,
            int seed,
            BushBranchBakeSettings branchSettings,
            int leafVariantCount)
        {
            return Extract(seed, branchSettings, leafVariantCount);
        }

        private static List<TipCandidate> FindTerminalTips(
            IReadOnlyList<BushBranchGenerator.Segment> segments, float depthThreshold, float attachBias)
        {
            // Non-terminal endpoints are any other segment's Start.
            var startPositions = new HashSet<Vector2Int>();
            foreach (var seg in segments)
            {
                startPositions.Add(Quantise(seg.Start));
            }

            var tips = new List<TipCandidate>(segments.Count);

            foreach (var seg in segments)
            {
                if (seg.Depth < depthThreshold)
                {
                    continue;
                }

                // Terminal: End isn't used as any other segment's Start.
                var endQ = Quantise(seg.End);
                if (startPositions.Contains(endQ))
                {
                    continue;
                }

                var attachPoint = Vector2.Lerp(seg.Start, seg.End, attachBias);

                tips.Add(new TipCandidate
                {
                    Position = attachPoint,
                    Depth = seg.Depth,
                    Angle = seg.DirectionAngle,
                    Score = seg.Depth
                });
            }

            return tips;
        }

        private static Vector2Int Quantise(Vector2 v)
        {
            // 1/4096 grid avoids float comparison issues.
            return new Vector2Int(
                Mathf.RoundToInt(v.x * 4096f),
                Mathf.RoundToInt(v.y * 4096f));
        }

        private static List<TipCandidate> SpatialFilter(
            List<TipCandidate> candidates, int maxCount)
        {
            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));

            var minDist = 1f / Mathf.Sqrt(maxCount) * 0.5f;
            var minDistSq = minDist * minDist;

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
                    if (candidate.Position.SqrDistance2D(existing.Position) < minDistSq)
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
            int leafVariantCount)
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
                    UVPosition = tip.Position,
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
            internal Vector2 Position;
            internal float Depth;
            internal float Angle;
            internal float Score;
        }
    }
}
