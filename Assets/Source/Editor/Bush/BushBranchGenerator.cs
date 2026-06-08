using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Generates fractal branch segments in UV space (0–1) for baking into
    /// a branch map texture. Produces a top-down radial structure: root at
    /// centre, branches radiate outward in all directions.
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

            var centre = new Vector2(0.5f, 0.5f);

            // Primary branches radiate from centre in evenly-spaced directions
            // with randomised jitter for organic feel
            var count = settings.BranchesPerNode;
            var baseAngleStep = Mathf.PI * 2f / count;

            // Small random rotation so the whole bush isn't axis-aligned
            var globalRotation = (float)rng.NextDouble() * Mathf.PI * 2f;

            for (var i = 0; i < count; i++)
            {
                var baseAngle = globalRotation + i * baseAngleStep;
                // Jitter the angle slightly for organic feel
                var jitter = (float)(rng.NextDouble() - 0.5) * baseAngleStep * 0.4f;
                var angle = baseAngle + jitter;

                var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var length = Lerp(rng, settings.TrunkLength * 0.5f, settings.TrunkLength);
                var end = centre + dir * length;

                end = ClampUV(end);

                segments.Add(new Segment
                {
                    Start = centre,
                    End = end,
                    StartWidth = settings.BranchWidth,
                    EndWidth = settings.BranchWidth * settings.WidthDecay,
                    Depth = 0f,
                    DirectionAngle = angle
                });

                GrowBranches(segments, rng, end, angle,
                    settings.BranchWidth * settings.WidthDecay, 1, settings);
            }

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
            // Reduce child count at deeper levels for natural thinning
            if (depth >= 3 && rng.NextDouble() < 0.3)
            {
                count = Mathf.Max(2, count - 1);
            }

            var normalizedDepth = (float)depth / settings.MaxDepth;

            for (var i = 0; i < count; i++)
            {
                var spreadMin = settings.AngleSpread.x * Mathf.Deg2Rad;
                var spreadMax = settings.AngleSpread.y * Mathf.Deg2Rad;
                var spread = Lerp(rng, spreadMin, spreadMax);

                // Alternate sign: even left, odd right; last gets small random offset
                var sign = (i % 2 == 0) ? -1f : 1f;
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

                end = ClampUV(end);

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

        private static Vector2 ClampUV(Vector2 v)
        {
            v.x = Mathf.Clamp(v.x, 0.03f, 0.97f);
            v.y = Mathf.Clamp(v.y, 0.03f, 0.97f);
            return v;
        }

        private static float Lerp(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }
    }
}

