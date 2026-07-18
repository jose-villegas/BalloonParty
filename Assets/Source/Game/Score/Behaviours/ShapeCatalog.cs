using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Game.Score.Behaviours
{
    /// <summary>
    ///     A CLOSED WALK a pen orbits forever: an ordered cyclic list of vertex indices whose consecutive pairs
    ///     (including the wrap from last back to first) are the edges to trace. A 2-vertex walk is a back-and-forth
    ///     shuttle (the degenerate loop that IS the "line" shape). <see cref="Arc"/> chooses interpolation: an arc
    ///     slerps the (sphere-surface) local positions so ring segments read as curved bands, a chord lerps them so
    ///     polyhedron edges read as straight lines. Segment count == <c>Vertices.Length</c> (cyclic).
    /// </summary>
    internal readonly struct FormationWalk
    {
        internal readonly int[] Vertices;
        internal readonly bool Arc;

        internal FormationWalk(int[] vertices, bool arc)
        {
            Vertices = vertices;
            Arc = arc;
        }
    }

    /// <summary>
    ///     A drawable formation: its vertex count (== its score denomination == its trail count), the local 3D
    ///     vertex positions (normalized to a unit bounding radius), and the closed walks its pens orbit. Pens are
    ///     distributed across walks proportionally to walk length (<see cref="PensPerWalk"/>, summing to the
    ///     denomination), spaced evenly along each walk, and orbit continuously during Draw + Collapse: the first
    ///     lap draws the shape, later laps re-ink it, and k pens sharing a period-P walk cover it in P/k.
    /// </summary>
    internal sealed class FormationShape
    {
        internal readonly int Denomination;
        internal readonly float RadiusScale;

        // The shape's local X aligns to the projectile hit direction at spawn (the line: its slope IS the
        // shot's linear equation); unaligned shapes start at a uniform random orientation instead.
        internal readonly bool AlignToHit;
        internal readonly Vector3[] Vertices;
        internal readonly FormationWalk[] Walks;
        internal readonly int[] PensPerWalk;

        // Per walk: the local perimeter and the cumulative local arc length at each segment boundary (length m+1,
        // [0] = 0, [m] = perimeter). Pens are parameterized by arc length so a WORLD-units/second pen speed means
        // the same travel speed between any two vertices, whatever the segment lengths.
        internal readonly float[] Perimeters;
        internal readonly float[][] Cumulative;

        internal FormationShape(
            int denomination, float radiusScale, Vector3[] vertices, FormationWalk[] walks, bool alignToHit = false)
        {
            Denomination = denomination;
            RadiusScale = radiusScale;
            AlignToHit = alignToHit;
            Vertices = vertices;
            Walks = walks;
            PensPerWalk = DistributePens(denomination, walks);

            Perimeters = new float[walks.Length];
            Cumulative = new float[walks.Length][];
            for (var w = 0; w < walks.Length; w++)
            {
                var loop = walks[w].Vertices;
                var m = loop.Length;
                var cumulative = new float[m + 1];
                for (var s = 0; s < m; s++)
                {
                    cumulative[s + 1] = cumulative[s] + Vector3.Distance(vertices[loop[s]], vertices[loop[(s + 1) % m]]);
                }

                Cumulative[w] = cumulative;
                Perimeters[w] = cumulative[m];
            }
        }

        // Largest-remainder apportionment of the pens over the walks by segment count, so the sum is exact and
        // longer walks get more pens (a pen never has zero to orbit for the authored catalog).
        private static int[] DistributePens(int total, FormationWalk[] walks)
        {
            var result = new int[walks.Length];
            var totalSegments = 0;
            for (var i = 0; i < walks.Length; i++)
            {
                totalSegments += walks[i].Vertices.Length;
            }

            var remainders = new float[walks.Length];
            var assigned = 0;
            for (var i = 0; i < walks.Length; i++)
            {
                var exact = (float)total * walks[i].Vertices.Length / totalSegments;
                result[i] = Mathf.FloorToInt(exact);
                remainders[i] = exact - result[i];
                assigned += result[i];
            }

            for (var leftover = total - assigned; leftover > 0; leftover--)
            {
                var best = 0;
                for (var i = 1; i < walks.Length; i++)
                {
                    if (remainders[i] > remainders[best])
                    {
                        best = i;
                    }
                }

                result[best]++;
                remainders[best] = -1f;
            }

            return result;
        }
    }

    /// <summary>
    ///     Hand-authored 3D shape data for BigScore formations. Each denomination maps to a shape whose vertex
    ///     count equals it — full 1:1 decomposition draws every point as one orbiting pen. Polyhedra partition
    ///     their edge set into closed walks (a Hamiltonian-ish cycle plus back-and-forth shuttles for the leftover
    ///     edges); spheres are latitude-ring loops (plus pole shuttles at high accuracy). The tables are built once
    ///     in the static constructor and returned by reference from <see cref="TryGet"/>, so a lookup never
    ///     allocates.
    ///
    ///     <see cref="Denominations"/> is the decomposition ladder (largest-first). The 12-vertex sphere the design
    ///     lists is deliberately absent from it: greedy over a 12-inclusive ladder splits 13 as 12+1, which
    ///     contradicts the required 13 = 10+3 decomposition (and 7 = 6+1 pins pure greedy, so no single rule
    ///     reconciles both once 12 is present). Spheres are 10/20/30 — three increasing accuracies.
    /// </summary>
    internal static class ShapeCatalog
    {
        private static readonly int[] LadderDenominations = { 30, 20, 10, 8, 6, 5, 4, 3, 2 };
        private static readonly Dictionary<int, FormationShape> Shapes = BuildShapes();

        internal static IReadOnlyList<int> Denominations => LadderDenominations;

        internal static bool TryGet(int denomination, out FormationShape shape)
        {
            return Shapes.TryGetValue(denomination, out shape);
        }

        private static Dictionary<int, FormationShape> BuildShapes()
        {
            return new Dictionary<int, FormationShape>
            {
                { 2, BuildLine() },
                { 3, BuildTriangle() },
                { 4, BuildTetrahedron() },
                { 5, BuildSquarePyramid() },
                { 6, BuildTriangularPrism() },
                { 8, BuildCube() },
                { 10, BuildSphere10() },
                { 20, BuildSphere20() },
                { 30, BuildSphere30() },
            };
        }

        // 2 = a single edge as a shuttle; two pens at opposite phases draw it inward from both ends.
        private static FormationShape BuildLine()
        {
            var vertices = new[] { new Vector3(-1f, 0f, 0f), new Vector3(1f, 0f, 0f) };
            var walks = new[] { Chord(0, 1) };
            return Build(2, 0.5f, vertices, walks, alignToHit: true);
        }

        // 3 = a flat equilateral triangle: one 3-cycle (the old star tier's triangle, now one loop, no nesting).
        private static FormationShape BuildTriangle()
        {
            var vertices = new[] { Polar(90f), Polar(210f), Polar(330f) };
            var walks = new[] { Chord(0, 1, 2) };
            return Build(3, 0.7f, vertices, walks);
        }

        // 4 = tetrahedron: the 4-cycle 0-1-2-3 plus the two diagonals 0-2 and 1-3 as shuttles = all 6 edges.
        private static FormationShape BuildTetrahedron()
        {
            var vertices = new[]
            {
                new Vector3(1f, 1f, 1f), new Vector3(1f, -1f, -1f),
                new Vector3(-1f, 1f, -1f), new Vector3(-1f, -1f, 1f),
            };
            var walks = new[] { Chord(0, 1, 2, 3), Chord(0, 2), Chord(1, 3) };
            return Build(4, 0.75f, vertices, walks);
        }

        // 5 = square pyramid: a 5-cycle 0-1-4-2-3 through the apex plus the three leftover edges (12, 04, 34) as
        // shuttles = base square {01,12,23,30} + apex spokes {04,14,24,34}.
        private static FormationShape BuildSquarePyramid()
        {
            var vertices = new[]
            {
                new Vector3(0.7f, 0.7f, -0.45f), new Vector3(-0.7f, 0.7f, -0.45f),
                new Vector3(-0.7f, -0.7f, -0.45f), new Vector3(0.7f, -0.7f, -0.45f),
                new Vector3(0f, 0f, 0.9f),
            };
            var walks = new[] { Chord(0, 1, 4, 2, 3), Chord(1, 2), Chord(0, 4), Chord(3, 4) };
            return Build(5, 0.8f, vertices, walks);
        }

        // 6 = triangular prism: the two triangles 0-1-2 and 3-4-5 as loops plus the three verticals (03, 14, 25)
        // as shuttles running the wide axis (José's "trails along the wide axis").
        private static FormationShape BuildTriangularPrism()
        {
            var vertices = new[]
            {
                PolarAt(90f, -0.6f), PolarAt(210f, -0.6f), PolarAt(330f, -0.6f),
                PolarAt(90f, 0.6f), PolarAt(210f, 0.6f), PolarAt(330f, 0.6f),
            };
            var walks = new[] { Chord(0, 1, 2), Chord(3, 4, 5), Chord(0, 3), Chord(1, 4), Chord(2, 5) };
            return Build(6, 0.85f, vertices, walks);
        }

        // 8 = cube: a Hamiltonian 8-cycle 0-1-2-3-7-6-5-4 plus the four leftover edges (30, 74, 15, 26) as shuttles.
        private static FormationShape BuildCube()
        {
            var vertices = new[]
            {
                new Vector3(-1f, -1f, -1f), new Vector3(1f, -1f, -1f),
                new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f),
                new Vector3(-1f, -1f, 1f), new Vector3(1f, -1f, 1f),
                new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f),
            };
            var walks = new[]
            {
                Chord(0, 1, 2, 3, 7, 6, 5, 4), Chord(3, 0), Chord(7, 4), Chord(1, 5), Chord(2, 6),
            };
            return Build(8, 0.9f, vertices, walks);
        }

        // 10 = 2 latitude ring loops of 5 (offset azimuth).
        private static FormationShape BuildSphere10()
        {
            var builder = new SphereBuilder();
            builder.AddRing(60f, 5, 0f);
            builder.AddRing(120f, 5, 36f);
            return builder.Build(10, 1f);
        }

        // 20 = 3 latitude ring loops 6/8/6 (top / equator / bottom).
        private static FormationShape BuildSphere20()
        {
            var builder = new SphereBuilder();
            builder.AddRing(45f, 6, 0f);
            builder.AddRing(90f, 8, 22.5f);
            builder.AddRing(135f, 6, 30f);
            return builder.Build(20, 1.15f);
        }

        // 30 = 4 latitude ring loops 6/8/8/6 plus 2 pole shuttles (meridian arcs) for a hint of longitude.
        private static FormationShape BuildSphere30()
        {
            var builder = new SphereBuilder();
            var north = builder.AddPole(1f);
            var ring1 = builder.AddRing(36f, 6, 0f);
            builder.AddRing(72f, 8, 22.5f);
            builder.AddRing(108f, 8, 0f);
            var ring4 = builder.AddRing(144f, 6, 30f);
            var south = builder.AddPole(-1f);
            builder.AddMeridianShuttle(north, ring1);
            builder.AddMeridianShuttle(south, ring4);
            return builder.Build(30, 1.3f);
        }

        private static FormationWalk Chord(params int[] vertices)
        {
            return new FormationWalk(vertices, arc: false);
        }

        // Unit direction in the XY plane at the given compass angle (flat shapes live at z = 0).
        private static Vector3 Polar(float degrees)
        {
            return PolarAt(degrees, 0f);
        }

        private static Vector3 PolarAt(float degrees, float z)
        {
            var rad = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), z);
        }

        // Normalizes vertices to a unit bounding radius so RadiusScale means the same thing across shapes.
        private static FormationShape Build(
            int denomination, float radiusScale, Vector3[] vertices, FormationWalk[] walks, bool alignToHit = false)
        {
            var maxMagnitude = 0f;
            for (var i = 0; i < vertices.Length; i++)
            {
                var m = vertices[i].magnitude;
                if (m > maxMagnitude)
                {
                    maxMagnitude = m;
                }
            }

            if (maxMagnitude > Mathf.Epsilon)
            {
                var inv = 1f / maxMagnitude;
                for (var i = 0; i < vertices.Length; i++)
                {
                    vertices[i] *= inv;
                }
            }

            return new FormationShape(denomination, radiusScale, vertices, walks, alignToHit);
        }

        // Accumulates sphere vertices ring by ring (already unit magnitude) and one loop walk per ring.
        private sealed class SphereBuilder
        {
            private readonly List<Vector3> _vertices = new();
            private readonly List<FormationWalk> _walks = new();

            // Returns the index of the added pole vertex so a meridian shuttle can attach to it.
            internal int AddPole(float z)
            {
                var index = _vertices.Count;
                _vertices.Add(new Vector3(0f, 0f, z));
                return index;
            }

            // Returns the first vertex index of the ring so callers can target it (e.g. a pole meridian).
            internal int AddRing(float polarDegrees, int count, float azimuthOffsetDegrees)
            {
                var first = _vertices.Count;
                var polar = polarDegrees * Mathf.Deg2Rad;
                var sin = Mathf.Sin(polar);
                var z = Mathf.Cos(polar);
                var loop = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var azimuth = (azimuthOffsetDegrees + 360f * i / count) * Mathf.Deg2Rad;
                    _vertices.Add(new Vector3(sin * Mathf.Cos(azimuth), sin * Mathf.Sin(azimuth), z));
                    loop[i] = first + i;
                }

                _walks.Add(new FormationWalk(loop, arc: true));
                return first;
            }

            // A pole + a ring vertex as a back-and-forth meridian arc (the pole's only walk).
            internal void AddMeridianShuttle(int poleIndex, int ringVertexIndex)
            {
                _walks.Add(new FormationWalk(new[] { poleIndex, ringVertexIndex }, arc: true));
            }

            internal FormationShape Build(int denomination, float radiusScale)
            {
                return new FormationShape(denomination, radiusScale, _vertices.ToArray(), _walks.ToArray());
            }
        }
    }
}
