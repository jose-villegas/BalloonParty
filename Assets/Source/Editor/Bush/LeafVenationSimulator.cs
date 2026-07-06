using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Editor.Bush
{
    /// <summary>
    /// Auxin-based venation simulator (Runions et al. 2005) for offline leaf baking.
    /// </summary>
    internal static class LeafVenationSimulator
    {
        private const int MaxBranchDepth = 3;
        private const int MaxBoundaryAttempts = 50;

        internal struct VeinNode
        {
            internal Vector2 Position;
            internal int Depth;
        }

        internal struct VeinSegment
        {
            internal Vector2 Start;
            internal Vector2 End;
            internal int Depth;
        }

        internal struct SimulationSettings
        {
            internal Vector2 LeafSize;
            internal float KillDistance;
            internal float AttractionDistance;
            internal int SourceCount;
            internal int MaxIterations;
            internal float StepSize;
            internal uint Seed;

            internal static SimulationSettings Default => new SimulationSettings
            {
                LeafSize = new Vector2(0.8f, 1.0f),
                KillDistance = 0.02f,
                AttractionDistance = 0.08f,
                SourceCount = 150,
                MaxIterations = 100,
                StepSize = 0.012f,
                Seed = 42
            };
        }

        /// <summary>
        /// Runs the simulation and returns vein segments with hierarchical depth (0 = midrib, 1+ = branches).
        /// </summary>
        internal static List<VeinSegment> Simulate(SimulationSettings settings)
        {
            var halfW = settings.LeafSize.x * 0.5f;
            var halfH = settings.LeafSize.y * 0.5f;

            var nodes = new List<VeinNode>();
            var segments = new List<VeinSegment>();

            BuildMidrib(nodes, segments, halfH, settings.StepSize);

            var sources = ScatterAuxinSources(
                settings.SourceCount, halfW, halfH, new System.Random((int)settings.Seed));

            for (var iter = 0; iter < settings.MaxIterations && sources.Count > 0; iter++)
            {
                var growthDirections = ComputeGrowthDirections(nodes, sources, settings.AttractionDistance);
                GrowNewNodes(nodes, segments, growthDirections, settings.StepSize, halfW, halfH);
                KillConsumedSources(sources, nodes, settings.KillDistance);
            }

            return segments;
        }

        /// <summary>
        /// Rasterises vein segments into a texture with depth-based thickness.
        /// </summary>
        internal static Texture2D RasteriseVeins(
            IReadOnlyList<VeinSegment> segments,
            int resolution,
            Vector2 leafSize,
            float midribWidth = 3f,
            Color? veinColor = null)
        {
            var tex = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            var pixels = new Color[resolution * resolution];

            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color(1f, 1f, 1f, 0f);
            }

            var color = veinColor ?? new Color(0.15f, 0.3f, 0.1f, 1f);
            var halfW = leafSize.x * 0.5f;
            var halfH = leafSize.y * 0.5f;

            foreach (var seg in segments)
            {
                var widthPx = midribWidth / (1f + seg.Depth * 0.8f);
                RasteriseLine(pixels, resolution, seg.Start, seg.End, halfW, halfH, widthPx, color);
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        private static void BuildMidrib(
            List<VeinNode> nodes, List<VeinSegment> segments,
            float halfH, float stepSize)
        {
            var midribSteps = Mathf.Max(4, Mathf.CeilToInt(halfH * 2f / stepSize * 0.15f));

            for (var i = 0; i <= midribSteps; i++)
            {
                var t = (float)i / midribSteps;
                var pos = new Vector2(0f, Mathf.Lerp(-halfH, halfH, t));

                nodes.Add(new VeinNode { Position = pos, Depth = 0 });

                if (i > 0)
                {
                    segments.Add(new VeinSegment
                    {
                        Start = nodes[i - 1].Position,
                        End = pos,
                        Depth = 0
                    });
                }
            }
        }

        private static List<Vector2> ScatterAuxinSources(
            int count, float halfW, float halfH, System.Random rng)
        {
            var sources = new List<Vector2>(count);

            for (var i = 0; i < count; i++)
            {
                var attempts = 0;
                Vector2 p;
                do
                {
                    p = new Vector2(
                        (float)(rng.NextDouble() * 2.0 - 1.0) * halfW,
                        (float)(rng.NextDouble() * 2.0 - 1.0) * halfH);
                    attempts++;
                }
                while (!IsInsideLeafBoundary(p, halfW, halfH) && attempts < MaxBoundaryAttempts);

                if (IsInsideLeafBoundary(p, halfW, halfH))
                {
                    sources.Add(p);
                }
            }

            return sources;
        }

        private static Dictionary<int, Vector2> ComputeGrowthDirections(
            IReadOnlyList<VeinNode> nodes,
            IReadOnlyList<Vector2> sources,
            float attractionDistance)
        {
            var closestNode = new int[sources.Count];
            var closestDist = new float[sources.Count];

            for (var s = 0; s < sources.Count; s++)
            {
                closestNode[s] = -1;
                closestDist[s] = float.MaxValue;
            }

            for (var n = 0; n < nodes.Count; n++)
            {
                for (var s = 0; s < sources.Count; s++)
                {
                    var dist = Vector2.Distance(nodes[n].Position, sources[s]);
                    if (dist < attractionDistance && dist < closestDist[s])
                    {
                        closestDist[s] = dist;
                        closestNode[s] = n;
                    }
                }
            }

            var growthDirections = new Dictionary<int, Vector2>();

            for (var s = 0; s < sources.Count; s++)
            {
                var n = closestNode[s];
                if (n < 0)
                {
                    continue;
                }

                var dir = (sources[s] - nodes[n].Position).normalized;
                if (growthDirections.ContainsKey(n))
                {
                    growthDirections[n] += dir;
                }
                else
                {
                    growthDirections[n] = dir;
                }
            }

            return growthDirections;
        }

        private static void GrowNewNodes(
            List<VeinNode> nodes, List<VeinSegment> segments,
            IReadOnlyDictionary<int, Vector2> growthDirections,
            float stepSize, float halfW, float halfH)
        {
            var pendingNodes = new List<(int parent, Vector2 pos, int depth)>();

            foreach (var kvp in growthDirections)
            {
                var parentNode = nodes[kvp.Key];
                var newPos = parentNode.Position + kvp.Value.normalized * stepSize;

                if (!IsInsideLeafBoundary(newPos, halfW, halfH))
                {
                    continue;
                }

                if (IsTooCloseToExistingNode(nodes, newPos, stepSize * 0.5f))
                {
                    continue;
                }

                var depth = Mathf.Min(parentNode.Depth + 1, MaxBranchDepth);
                pendingNodes.Add((kvp.Key, newPos, depth));
            }

            foreach (var (parent, pos, depth) in pendingNodes)
            {
                nodes.Add(new VeinNode { Position = pos, Depth = depth });
                segments.Add(new VeinSegment
                {
                    Start = nodes[parent].Position,
                    End = pos,
                    Depth = depth
                });
            }
        }

        private static bool IsTooCloseToExistingNode(
            IReadOnlyList<VeinNode> nodes, Vector2 position, float minDistance)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (Vector2.Distance(nodes[i].Position, position) < minDistance)
                {
                    return true;
                }
            }

            return false;
        }

        private static void KillConsumedSources(
            List<Vector2> sources, IReadOnlyList<VeinNode> nodes, float killDistance)
        {
            for (var s = sources.Count - 1; s >= 0; s--)
            {
                for (var n = 0; n < nodes.Count; n++)
                {
                    if (Vector2.Distance(nodes[n].Position, sources[s]) < killDistance)
                    {
                        sources.RemoveAt(s);
                        break;
                    }
                }
            }
        }

        private static bool IsInsideLeafBoundary(Vector2 p, float halfW, float halfH)
        {
            // Tapered ellipse to approximate a natural leaf silhouette.
            var ny = (p.y / halfH + 1f) * 0.5f;
            var taper = Mathf.Sin(ny * Mathf.PI) * 0.9f + 0.1f;
            var ex = p.x / (halfW * taper);
            var ey = p.y / halfH;
            return ex * ex + ey * ey <= 1f;
        }

        private static void RasteriseLine(
            Color[] pixels, int res,
            Vector2 start, Vector2 end,
            float halfW, float halfH,
            float widthPx, Color color)
        {
            var p0 = WorldToPixel(start, halfW, halfH, res);
            var p1 = WorldToPixel(end, halfW, halfH, res);

            var dist = Vector2.Distance(p0, p1);
            var steps = Mathf.CeilToInt(dist * 2f);

            for (var i = 0; i <= steps; i++)
            {
                var t = steps > 0 ? (float)i / steps : 0f;
                var center = Vector2.Lerp(p0, p1, t);
                StampCircle(pixels, res, center, widthPx * 0.5f, color);
            }
        }

        private static void StampCircle(
            Color[] pixels, int res,
            Vector2 center, float halfWidth, Color color)
        {
            var minX = Mathf.Max(0, Mathf.FloorToInt(center.x - halfWidth - 1));
            var maxX = Mathf.Min(res - 1, Mathf.CeilToInt(center.x + halfWidth + 1));
            var minY = Mathf.Max(0, Mathf.FloorToInt(center.y - halfWidth - 1));
            var maxY = Mathf.Min(res - 1, Mathf.CeilToInt(center.y + halfWidth + 1));

            for (var y = minY; y <= maxY; y++)
            {
                for (var x = minX; x <= maxX; x++)
                {
                    var d = Vector2.Distance(new Vector2(x, y), center);
                    var aa = 1f - Mathf.Clamp01((d - halfWidth + 0.5f) / 1f);

                    if (aa <= 0f)
                    {
                        continue;
                    }

                    var idx = y * res + x;
                    var existing = pixels[idx];
                    var blended = Color.Lerp(existing, color, aa * color.a);
                    blended.a = Mathf.Max(existing.a, aa * color.a);
                    pixels[idx] = blended;
                }
            }
        }

        private static Vector2 WorldToPixel(Vector2 world, float halfW, float halfH, int res)
        {
            var u = (world.x / halfW + 1f) * 0.5f;
            var v = (world.y / halfH + 1f) * 0.5f;
            return new Vector2(u * (res - 1), v * (res - 1));
        }
    }
}
