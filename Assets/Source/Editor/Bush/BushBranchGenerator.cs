using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Generates fractal branch segments in UV space (0–1) for baking into
    /// a branch map texture. Uses recursive subdivision with deterministic
    /// seeded randomisation.
    /// </summary>
    internal static class BushBranchGenerator
    {
        internal struct Segment
        {
            internal Vector2 Start;
            internal Vector2 End;
            internal float StartWidth;
            internal float EndWidth;
            internal float Depth;
            internal float DirectionAngle;
        }

        internal static List<Segment> Generate(int seed, BushBranchBakeSettings settings)
        {
            var segments = new List<Segment>(64);
            var rng = new System.Random(seed);

            var rootPos = new Vector2(0.5f, 0.15f);
            var trunkEnd = rootPos + Vector2.up * settings.TrunkLength;

            segments.Add(new Segment
            {
                Start = rootPos,
                End = trunkEnd,
                StartWidth = settings.BranchWidth,
                EndWidth = settings.BranchWidth * settings.WidthDecay,
                Depth = 0f,
                DirectionAngle = Mathf.PI * 0.5f
            });

            GrowBranches(segments, rng, trunkEnd, Mathf.PI * 0.5f,
                settings.BranchWidth * settings.WidthDecay, 1, settings);

            return segments;
        }

        private static void GrowBranches(
            List<Segment> segments,
            System.Random rng,
            Vector2 origin,
            float parentAngle,
            float parentWidth,
            int depth,
            BushBranchBakeSettings settings)
        {
            if (depth > settings.MaxDepth)
            {
                return;
            }

            var count = settings.BranchesPerNode;
            var normalizedDepth = (float)depth / settings.MaxDepth;

            for (var i = 0; i < count; i++)
            {
                var spreadMin = settings.AngleSpread.x * Mathf.Deg2Rad;
                var spreadMax = settings.AngleSpread.y * Mathf.Deg2Rad;
                var spread = Lerp(rng, spreadMin, spreadMax);

                // Alternate sign: even indices go left, odd go right
                var sign = (i % 2 == 0) ? -1f : 1f;
                // Centre branch (if odd count, last child) gets reduced spread
                if (i == count - 1 && count % 2 == 1)
                {
                    sign = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.3f;
                }

                var angle = parentAngle + sign * spread;

                var lengthMin = settings.LengthRange.x;
                var lengthMax = settings.LengthRange.y;
                var length = Lerp(rng, lengthMin, lengthMax) * Mathf.Pow(settings.LengthDecay, depth - 1);

                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var end = origin + dir * length;

                end.x = Mathf.Clamp(end.x, 0.03f, 0.97f);
                end.y = Mathf.Clamp(end.y, 0.03f, 0.97f);

                var startWidth = parentWidth;
                var endWidth = startWidth * settings.TipTaper;

                segments.Add(new Segment
                {
                    Start = origin,
                    End = end,
                    StartWidth = startWidth,
                    EndWidth = endWidth,
                    Depth = normalizedDepth,
                    DirectionAngle = angle
                });

                var childWidth = startWidth * settings.WidthDecay;
                GrowBranches(segments, rng, end, angle, childWidth, depth + 1, settings);
            }
        }

        private static float Lerp(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}

