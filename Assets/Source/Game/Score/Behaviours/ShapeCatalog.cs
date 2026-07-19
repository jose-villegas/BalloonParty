using System;
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
    ///     count equals it — full 1:1 decomposition draws every point as one orbiting pen. Small shapes partition
    ///     their edge set into closed walks (a Hamiltonian-ish cycle plus back-and-forth shuttles for the leftover
    ///     edges); 10 is a latitude-ring globe; the crown of the ladder is a golden-ratio star-and-solid family —
    ///     12 the small stellated dodecahedron, 20 the dodecahedron, 30 the dodecadodecahedron, 50 the
    ///     rhombicosacron, and 100 the grand antiprism (a 4-polytope, projected to 3D at build time). The tables
    ///     are built once in the static constructor and returned by reference from <see cref="TryGet"/>, so a
    ///     lookup never allocates.
    ///
    ///     <see cref="Denominations"/> is the decomposition ladder (largest-first) the optimal coin-change split
    ///     (<see cref="BigScoreTrailBehaviour.Decompose"/>) draws its pieces from.
    /// </summary>
    internal static class ShapeCatalog
    {
        // φ (golden ratio) — the vertex coordinates the star-and-solid family (12/20/30/50/100) is framed on.
        private const float Phi = 1.6180339887498949f;

        private static readonly int[] LadderDenominations = { 100, 50, 30, 20, 12, 10, 8, 6, 5, 4, 3, 2 };

        // The 12 even permutations of four positions (A4) — the 600-cell's 96-vertex orbit is the EVEN cyclic
        // arrangements of (±φ/2, ±1/2, ±1/(2φ), 0); odd arrangements are NOT vertices.
        private static readonly int[][] EvenPermutations4 =
        {
            new[] { 0, 1, 2, 3 }, new[] { 0, 2, 3, 1 }, new[] { 0, 3, 1, 2 },
            new[] { 1, 0, 3, 2 }, new[] { 1, 2, 0, 3 }, new[] { 1, 3, 2, 0 },
            new[] { 2, 0, 1, 3 }, new[] { 2, 1, 3, 0 }, new[] { 2, 3, 0, 1 },
            new[] { 3, 0, 2, 1 }, new[] { 3, 1, 0, 2 }, new[] { 3, 2, 1, 0 },
        };

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
                { 10, BuildOctagonalBipyramid() },
                { 12, BuildSmallStellatedDodecahedron() },
                { 20, BuildDodecahedron() },
                { 30, BuildDodecadodecahedron() },
                { 50, BuildRhombicosacron() },
                { 100, BuildGrandAntiprism() },
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

        // 12 = small stellated dodecahedron: the icosahedron's 12 vertices framed by 12 pentagram circuits — one
        // per vertex, tracing that vertex's five neighbours in {5/2} (skip-2) order, one pen apiece. Every chord of
        // the frame belongs to two adjacent circuits, so the star is deliberately DOUBLE-INKED (a brighter frame).
        private static FormationShape BuildSmallStellatedDodecahedron()
        {
            var vertices = IcosahedronVertices();
            var walks = PentagramCircuits(vertices);
            return Build(12, 1.1f, vertices, walks);
        }

        // 10 = octagonal bipyramid: an 8-vertex equator ring (radius 0.8 — taller than wide) + two
        // apexes. All degrees are even (ring 4, poles 8), so like the rhombicosacron it draws
        // SINGLE-inked: the equator octagon is one loop, and ONE pole-to-pole zigzag
        // (top→v0→bottom→v1→top→…) threads all 16 fan edges exactly once.
        private static FormationShape BuildOctagonalBipyramid()
        {
            const int ringCount = 8;
            var vertices = new Vector3[ringCount + 2];
            var ring = new int[ringCount];
            for (var i = 0; i < ringCount; i++)
            {
                var azimuth = 2f * Mathf.PI * i / ringCount;
                vertices[i] = new Vector3(Mathf.Cos(azimuth) * 0.8f, Mathf.Sin(azimuth) * 0.8f, 0f);
                ring[i] = i;
            }

            vertices[8] = new Vector3(0f, 0f, 1f);
            vertices[9] = new Vector3(0f, 0f, -1f);

            var zigzag = new int[2 * ringCount];
            for (var i = 0; i < ringCount; i++)
            {
                zigzag[2 * i] = i % 2 == 0 ? 8 : 9;
                zigzag[2 * i + 1] = i;
            }

            var walks = new[] { Chord(ring), Chord(zigzag) };
            return Build(10, 1f, vertices, walks);
        }

        // 20 = regular dodecahedron: its 12 pentagon faces as 5-loops. Every edge is shared by two faces, so the
        // frame is double-inked (like the stellated 12). The stellated 12's dual, completing the star-and-solid pair.
        private static FormationShape BuildDodecahedron()
        {
            var vertices = DodecahedronVertices();
            var walks = CoplanarFaceWalks(vertices, IcosahedralFaceNormals(), allowPentagrams: false);
            return Build(20, 1.15f, vertices, walks);
        }

        // 30 = dodecadodecahedron: the icosidodecahedron's 30 vertices, 24 face circuits = 12 pentagons {5} + 12
        // pentagrams {5/2}. Every edge belongs to one pentagon and one pentagram, so each is double-inked (as the
        // stellated 12). The ladder's near-crown — a uniform star polyhedron of interpenetrating loops.
        private static FormationShape BuildDodecadodecahedron()
        {
            var vertices = IcosidodecahedronVertices();
            var walks = CoplanarFaceWalks(vertices, IcosahedralFaceNormals(), allowPentagrams: true);
            return Build(30, 1.3f, vertices, walks);
        }

        // 50 = rhombicosacron (dual of the uniform rhombicosahedron): 50 vertices along the icosahedral 3-fold axes
        // (20 face centres, degree 6) and 2-fold axes (30 edge midpoints, degree 4). All degrees are even, so the
        // 120-edge graph is Eulerian and partitions into edge-disjoint closed circuits — every edge inked EXACTLY
        // ONCE (unlike the double-inked face-circuit siblings above). The ladder's crown.
        private static FormationShape BuildRhombicosacron()
        {
            var vertices = RhombicosacronVertices(IcosahedronVertices());
            var walks = EulerianCircuits(vertices);
            return Build(50, 1.45f, vertices, walks);
        }

        // 100 = grand antiprism, the exceptional uniform 4-POLYTOPE: the 600-cell's 120 unit vertices minus two
        // completely orthogonal great-decagon rings (10 + 10). The 500 surviving edges (degree 10 everywhere, all
        // even → Eulerian) partition into edge-disjoint closed circuits, every edge inked exactly once, and the 4D
        // frame is perspective-projected from w to 3D at build time for the layered-shell look. The ladder's crown.
        private static FormationShape BuildGrandAntiprism()
        {
            var cell = Cell600Vertices();
            var survivors = RemoveOrthogonalDecagons(cell);
            var vertices = ProjectPerspectiveFromW(survivors);

            // Edges live in 4D (uniform there; the projection stretches them in 3D, deliberately).
            var adjacency = EdgeGraph4(survivors, 0.5f * Phi);
            RequireUniformDegree(adjacency, 10, "grand antiprism");
            Require(CountEdges(adjacency) == 500, "grand antiprism must have exactly 500 edges");

            var cycles = PartitionIntoCycles(adjacency);
            MergeUndersizedCycles(cycles, minLength: 5);
            return Build(100, 1.6f, vertices, ToChordWalks(cycles));
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

        // The 12 icosahedron vertices as the cyclic permutations of (0, ±1, ±φ), normalized to the unit sphere.
        private static Vector3[] IcosahedronVertices()
        {
            var vertices = new[]
            {
                new Vector3(0f, 1f, Phi), new Vector3(0f, 1f, -Phi),
                new Vector3(0f, -1f, Phi), new Vector3(0f, -1f, -Phi),
                new Vector3(1f, Phi, 0f), new Vector3(1f, -Phi, 0f),
                new Vector3(-1f, Phi, 0f), new Vector3(-1f, -Phi, 0f),
                new Vector3(Phi, 0f, 1f), new Vector3(Phi, 0f, -1f),
                new Vector3(-Phi, 0f, 1f), new Vector3(-Phi, 0f, -1f),
            };

            for (var i = 0; i < vertices.Length; i++)
            {
                vertices[i].Normalize();
            }

            return vertices;
        }

        // One pentagram circuit per vertex: the vertex's five neighbours traced {5/2}. The circuits share every
        // chord pairwise, so together they double-ink all 30 edges of the small stellated dodecahedron's frame.
        private static FormationWalk[] PentagramCircuits(Vector3[] vertices)
        {
            var walks = new FormationWalk[vertices.Length];
            for (var v = 0; v < vertices.Length; v++)
            {
                var ring = OrderNeighbourRing(v, vertices);
                // Skip-2 over the cyclically ordered ring turns the neighbour pentagon into a pentagram.
                walks[v] = Chord(ring[0], ring[2], ring[4], ring[1], ring[3]);
            }

            return walks;
        }

        // The five nearest vertices to the centre, ordered cyclically by angle in the centre's tangent plane.
        private static int[] OrderNeighbourRing(int center, Vector3[] vertices)
        {
            var centerPosition = vertices[center];
            var neighbours = new List<int>(vertices.Length - 1);
            for (var i = 0; i < vertices.Length; i++)
            {
                if (i != center)
                {
                    neighbours.Add(i);
                }
            }

            neighbours.Sort((a, b) => (vertices[a] - centerPosition).sqrMagnitude
                .CompareTo((vertices[b] - centerPosition).sqrMagnitude));
            neighbours.RemoveRange(5, neighbours.Count - 5);

            var normal = centerPosition.normalized;
            var reference = Vector3.ProjectOnPlane(vertices[neighbours[0]] - centerPosition, normal).normalized;
            var binormal = Vector3.Cross(normal, reference);
            neighbours.Sort((a, b) => RingAngle(vertices[a] - centerPosition, reference, binormal)
                .CompareTo(RingAngle(vertices[b] - centerPosition, reference, binormal)));

            return neighbours.ToArray();
        }

        private static float RingAngle(Vector3 spoke, Vector3 reference, Vector3 binormal)
        {
            return Mathf.Atan2(Vector3.Dot(spoke, binormal), Vector3.Dot(spoke, reference));
        }

        // The 20 dodecahedron vertices: the 8 cube corners (±1, ±1, ±1) plus the 12 cyclic permutations of
        // (0, ±1/φ, ±φ), normalized to the unit sphere.
        private static Vector3[] DodecahedronVertices()
        {
            var vertices = new List<Vector3>(20);
            for (var sx = -1; sx <= 1; sx += 2)
            {
                for (var sy = -1; sy <= 1; sy += 2)
                {
                    for (var sz = -1; sz <= 1; sz += 2)
                    {
                        vertices.Add(new Vector3(sx, sy, sz));
                    }
                }
            }

            var invPhi = 1f / Phi;
            for (var shift = 0; shift < 3; shift++)
            {
                for (var sb = -1; sb <= 1; sb += 2)
                {
                    for (var sc = -1; sc <= 1; sc += 2)
                    {
                        vertices.Add(Cyclic(0f, sb * invPhi, sc * Phi, shift));
                    }
                }
            }

            return Normalized(vertices);
        }

        // The 30 icosidodecahedron vertices (shared by the dodecadodecahedron): the 6 axis points (cyclic
        // permutations of (0, 0, ±φ)) plus the 24 even/cyclic permutations of (±1/2, ±φ/2, ±φ²/2), normalized.
        private static Vector3[] IcosidodecahedronVertices()
        {
            var vertices = new List<Vector3>(30);
            for (var sign = -1; sign <= 1; sign += 2)
            {
                vertices.Add(new Vector3(sign * Phi, 0f, 0f));
                vertices.Add(new Vector3(0f, sign * Phi, 0f));
                vertices.Add(new Vector3(0f, 0f, sign * Phi));
            }

            var half = 0.5f;
            var halfPhi = 0.5f * Phi;
            var halfPhiSq = 0.5f * Phi * Phi;
            for (var shift = 0; shift < 3; shift++)
            {
                for (var sa = -1; sa <= 1; sa += 2)
                {
                    for (var sb = -1; sb <= 1; sb += 2)
                    {
                        for (var sc = -1; sc <= 1; sc += 2)
                        {
                            vertices.Add(Cyclic(sa * half, sb * halfPhi, sc * halfPhiSq, shift));
                        }
                    }
                }
            }

            return Normalized(vertices);
        }

        // The 12 five-fold face axes shared by the dodecahedron (20) and dodecadodecahedron (30): the cyclic
        // permutations of (0, ±φ, ±1), normalized. (The conjugate of the stellated 12's (0, ±1, ±φ) icosahedron.)
        private static Vector3[] IcosahedralFaceNormals()
        {
            var normals = new List<Vector3>(12);
            for (var shift = 0; shift < 3; shift++)
            {
                for (var sb = -1; sb <= 1; sb += 2)
                {
                    for (var sc = -1; sc <= 1; sc += 2)
                    {
                        normals.Add(Cyclic(0f, sb * Phi, sc * 1f, shift));
                    }
                }
            }

            return Normalized(normals);
        }

        // Slices the vertex set with each face plane: the 5 coplanar vertices on a plane, ordered around its normal,
        // are one face circuit. A pentagon connects them in angle order; a pentagram (star faces only) skips 2. The
        // faces are the buckets whose in-plane edge equals the polyhedron's single edge length — pentagons' at that
        // length, pentagrams' one skip-2 chord along; other coplanar bands (the dodecahedron's) are discarded.
        private static FormationWalk[] CoplanarFaceWalks(
            Vector3[] vertices, Vector3[] normals, bool allowPentagrams)
        {
            var rings = CollectFacePlaneRings(vertices, normals, out var adjacent);

            // Pentagons are the wider faces (adjacent chord == edge); pentagrams' adjacent chord is edge / φ, so
            // their skip-2 chord is the edge. Without pentagrams, the narrower non-face bands are dropped.
            var edge = allowPentagrams ? Max(adjacent) : Min(adjacent);
            var walks = new List<FormationWalk>(rings.Count);
            for (var k = 0; k < rings.Count; k++)
            {
                var ring = rings[k];
                if (Mathf.Abs(adjacent[k] - edge) < 1e-3f)
                {
                    walks.Add(Chord(ring));
                }
                else if (allowPentagrams && Mathf.Abs(adjacent[k] * Phi - edge) < 1e-3f)
                {
                    walks.Add(Chord(ring[0], ring[2], ring[4], ring[1], ring[3]));
                }
            }

            return walks.ToArray();
        }

        // Every plane (a face normal at a distinct positive height) holding exactly 5 vertices is a candidate face
        // ring, ordered around its normal; the parallel adjacent list carries each ring's first-edge chord length.
        private static List<int[]> CollectFacePlaneRings(
            Vector3[] vertices, Vector3[] normals, out List<float> adjacent)
        {
            var rings = new List<int[]>();
            adjacent = new List<float>();
            var seen = new HashSet<long>();
            foreach (var normal in normals)
            {
                var buckets = new Dictionary<int, List<int>>();
                for (var idx = 0; idx < vertices.Length; idx++)
                {
                    var height = Vector3.Dot(vertices[idx], normal);
                    if (height <= 1e-4f)
                    {
                        continue;
                    }

                    var key = Mathf.RoundToInt(height * 10000f);
                    if (!buckets.TryGetValue(key, out var members))
                    {
                        members = new List<int>();
                        buckets[key] = members;
                    }

                    members.Add(idx);
                }

                foreach (var members in buckets.Values)
                {
                    if (members.Count == 5 && seen.Add(RingMask(members)))
                    {
                        var ring = OrderByAngle(members, normal, vertices);
                        rings.Add(ring);
                        adjacent.Add(Vector3.Distance(vertices[ring[0]], vertices[ring[1]]));
                    }
                }
            }

            return rings;
        }

        private static long RingMask(IReadOnlyList<int> members)
        {
            var mask = 0L;
            for (var i = 0; i < members.Count; i++)
            {
                mask |= 1L << members[i];
            }

            return mask;
        }

        // Orders coplanar member vertices cyclically by angle in the plane perpendicular to the face normal.
        private static int[] OrderByAngle(List<int> members, Vector3 axis, Vector3[] vertices)
        {
            var normal = axis.normalized;
            var reference = Vector3.ProjectOnPlane(vertices[members[0]], normal).normalized;
            var binormal = Vector3.Cross(normal, reference);
            members.Sort((a, b) => RingAngle(vertices[a], reference, binormal)
                .CompareTo(RingAngle(vertices[b], reference, binormal)));
            return members.ToArray();
        }

        // The 50 rhombicosacron vertices from one icosahedron: the 20 face centroids (3-fold axes) then the 30 edge
        // midpoints (2-fold axes), each normalized. Two orbits at two distinct pre-normalization radii.
        private static Vector3[] RhombicosacronVertices(Vector3[] icosahedron)
        {
            var midpoints = IcosahedronEdgeMidpoints(icosahedron, out var neighbours);
            var centroids = IcosahedronFaceCentroids(icosahedron, neighbours);

            var vertices = new List<Vector3>(50);
            vertices.AddRange(centroids);
            vertices.AddRange(midpoints);
            return Normalized(vertices);
        }

        // The 30 icosahedron edge midpoints (the 2-fold axes), also returning the vertex adjacency the face
        // centroids are traced from. Edges are the vertex pairs at the minimum (edge) separation.
        private static List<Vector3> IcosahedronEdgeMidpoints(Vector3[] icosahedron, out List<int>[] neighbours)
        {
            neighbours = new List<int>[icosahedron.Length];
            for (var i = 0; i < icosahedron.Length; i++)
            {
                neighbours[i] = new List<int>();
            }

            var edgeLength = float.MaxValue;
            for (var i = 0; i < icosahedron.Length; i++)
            {
                for (var j = i + 1; j < icosahedron.Length; j++)
                {
                    edgeLength = Mathf.Min(edgeLength, Vector3.Distance(icosahedron[i], icosahedron[j]));
                }
            }

            var midpoints = new List<Vector3>(30);
            for (var i = 0; i < icosahedron.Length; i++)
            {
                for (var j = i + 1; j < icosahedron.Length; j++)
                {
                    if (Mathf.Abs(Vector3.Distance(icosahedron[i], icosahedron[j]) - edgeLength) < 1e-3f)
                    {
                        neighbours[i].Add(j);
                        neighbours[j].Add(i);
                        midpoints.Add((icosahedron[i] + icosahedron[j]) * 0.5f);
                    }
                }
            }

            return midpoints;
        }

        // The 20 icosahedron face centroids (the 3-fold axes): each mutually-adjacent triple of vertices, counted
        // once via i < j < k.
        private static List<Vector3> IcosahedronFaceCentroids(Vector3[] icosahedron, List<int>[] neighbours)
        {
            var centroids = new List<Vector3>(20);
            for (var i = 0; i < icosahedron.Length; i++)
            {
                for (var a = 0; a < neighbours[i].Count; a++)
                {
                    for (var b = a + 1; b < neighbours[i].Count; b++)
                    {
                        var j = neighbours[i][a];
                        var k = neighbours[i][b];
                        if (i < j && i < k && neighbours[j].Contains(k))
                        {
                            centroids.Add((icosahedron[i] + icosahedron[j] + icosahedron[k]) / 3f);
                        }
                    }
                }
            }

            return centroids;
        }

        // Partitions the rhombicosacron's edge graph into edge-disjoint closed circuits. Two vertices are adjacent
        // when their axes meet at the icosahedral 3-fold/2-fold incidence angle (dot == 1/√3): every vertex then has
        // even degree (6 on the 20 face-axis verts, 4 on the 30 edge-axis verts), so the graph is Eulerian. A
        // greedy trace splits off a simple cycle whenever the walk revisits a vertex — moderate-length circuits,
        // each edge inked exactly once.
        private static FormationWalk[] EulerianCircuits(Vector3[] vertices)
        {
            return ToChordWalks(PartitionIntoCycles(BuildIncidenceGraph(vertices)));
        }

        // Partitions an even-degree (Eulerian) edge graph into edge-disjoint closed cycles — shared by the
        // rhombicosacron (50) and the grand antiprism (100).
        private static List<List<int>> PartitionIntoCycles(List<int>[] adjacency)
        {
            var count = adjacency.Length;
            var used = new HashSet<long>();
            var cycles = new List<List<int>>();
            for (var start = 0; start < count; start++)
            {
                while (UnusedNeighbour(adjacency, used, start, count) != -1)
                {
                    TraceCircuits(adjacency, used, start, count, cycles);
                }
            }

            return cycles;
        }

        // DistributePens seeds each walk with floor(length · pens / totalSegments) pens before remainders, so on
        // a dense frame (the grand antiprism: 100 pens over 500 segments) a cycle shorter than pens-per-segment⁻¹
        // can end up with ZERO pens — a circuit that would never be inked. Splice each undersized cycle into a
        // host cycle at a shared vertex (the host detours around the small loop mid-walk): edge-disjointness, and
        // thus single-inking, is preserved, and every walk is long enough to earn a pen.
        private static void MergeUndersizedCycles(List<List<int>> cycles, int minLength)
        {
            while (true)
            {
                var smallIndex = IndexOfUndersized(cycles, minLength);
                if (smallIndex == -1)
                {
                    return;
                }

                var small = cycles[smallIndex];
                var hostIndex = FindSpliceHost(cycles, smallIndex, out var hostAt, out var smallAt);
                var host = cycles[hostIndex];

                // Insert the small cycle after the shared vertex, rotated to start just past it and ending back
                // on it, so the host's own edges resume unchanged.
                var detour = new List<int>(small.Count);
                for (var t = 1; t <= small.Count; t++)
                {
                    detour.Add(small[(smallAt + t) % small.Count]);
                }

                host.InsertRange(hostAt + 1, detour);
                cycles.RemoveAt(smallIndex);
            }
        }

        private static int IndexOfUndersized(List<List<int>> cycles, int minLength)
        {
            for (var i = 0; i < cycles.Count; i++)
            {
                if (cycles[i].Count < minLength)
                {
                    return i;
                }
            }

            return -1;
        }

        // A vertex on an undersized cycle always sits on other circuits too (its degree exceeds the cycle's two
        // edges), so a host sharing a vertex is guaranteed for the authored graphs.
        private static int FindSpliceHost(List<List<int>> cycles, int smallIndex, out int hostAt, out int smallAt)
        {
            var small = cycles[smallIndex];
            for (var j = 0; j < cycles.Count; j++)
            {
                if (j == smallIndex)
                {
                    continue;
                }

                var candidate = cycles[j];
                for (var h = 0; h < candidate.Count; h++)
                {
                    var s = small.IndexOf(candidate[h]);
                    if (s != -1)
                    {
                        hostAt = h;
                        smallAt = s;
                        return j;
                    }
                }
            }

            throw new InvalidOperationException("ShapeCatalog: an undersized cycle shares no vertex with any host");
        }

        private static FormationWalk[] ToChordWalks(List<List<int>> cycles)
        {
            var walks = new FormationWalk[cycles.Count];
            for (var i = 0; i < cycles.Count; i++)
            {
                walks[i] = Chord(cycles[i].ToArray());
            }

            return walks;
        }

        // Two vertices are adjacent when their axes meet at the icosahedral 3-fold/2-fold incidence angle (dot 1/√3).
        private static List<int>[] BuildIncidenceGraph(Vector3[] vertices)
        {
            var count = vertices.Length;
            var adjacency = new List<int>[count];
            for (var i = 0; i < count; i++)
            {
                adjacency[i] = new List<int>();
            }

            var incidence = 1f / Mathf.Sqrt(3f);
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    if (Mathf.Abs(Vector3.Dot(vertices[i], vertices[j]) - incidence) < 1e-3f)
                    {
                        adjacency[i].Add(j);
                        adjacency[j].Add(i);
                    }
                }
            }

            return adjacency;
        }

        // Walks unused edges from start, splitting off a simple cycle each time the walk revisits a path vertex,
        // until start is exhausted. Every edge consumed lands in exactly one emitted cycle.
        private static void TraceCircuits(
            List<int>[] adjacency, HashSet<long> used, int start, int count, List<List<int>> cycles)
        {
            var path = new List<int> { start };
            var indexInPath = new Dictionary<int, int> { { start, 0 } };
            var current = start;
            while (true)
            {
                var next = UnusedNeighbour(adjacency, used, current, count);
                if (next == -1)
                {
                    break;
                }

                used.Add(EdgeKey(current, next, count));
                current = next;
                if (!indexInPath.TryGetValue(next, out var loopStart))
                {
                    indexInPath[next] = path.Count;
                    path.Add(next);
                    continue;
                }

                var cycle = new List<int>(path.Count - loopStart);
                for (var t = loopStart; t < path.Count; t++)
                {
                    cycle.Add(path[t]);
                    if (t > loopStart)
                    {
                        indexInPath.Remove(path[t]);
                    }
                }

                cycles.Add(cycle);
                path.RemoveRange(loopStart + 1, path.Count - (loopStart + 1));
            }
        }

        private static int UnusedNeighbour(List<int>[] adjacency, HashSet<long> used, int vertex, int count)
        {
            var neighbours = adjacency[vertex];
            for (var i = 0; i < neighbours.Count; i++)
            {
                if (!used.Contains(EdgeKey(vertex, neighbours[i], count)))
                {
                    return neighbours[i];
                }
            }

            return -1;
        }

        private static long EdgeKey(int a, int b, int count)
        {
            return a < b ? (long)a * count + b : (long)b * count + a;
        }

        // The 600-cell's 120 unit vertices (the icosians): 8 permutations of (±1, 0, 0, 0), 16 of
        // (±1/2, ±1/2, ±1/2, ±1/2), and 96 EVEN permutations of (±φ/2, ±1/2, ±1/(2φ), 0).
        private static Vector4[] Cell600Vertices()
        {
            var vertices = new List<Vector4>(120);
            for (var axis = 0; axis < 4; axis++)
            {
                for (var sign = -1; sign <= 1; sign += 2)
                {
                    var v = Vector4.zero;
                    v[axis] = sign;
                    vertices.Add(v);
                }
            }

            for (var bits = 0; bits < 16; bits++)
            {
                vertices.Add(new Vector4(
                    (bits & 1) == 0 ? 0.5f : -0.5f,
                    (bits & 2) == 0 ? 0.5f : -0.5f,
                    (bits & 4) == 0 ? 0.5f : -0.5f,
                    (bits & 8) == 0 ? 0.5f : -0.5f));
            }

            AddEvenPermutationOrbit(vertices);

            var result = vertices.ToArray();
            Require(result.Length == 120, "the 600-cell must have 120 vertices");
            for (var i = 0; i < result.Length; i++)
            {
                Require(Mathf.Abs(result[i].magnitude - 1f) < 1e-4f, "600-cell vertices must be unit icosians");
                for (var j = i + 1; j < result.Length; j++)
                {
                    Require((result[i] - result[j]).sqrMagnitude > 1e-8f, "600-cell vertices must be distinct");
                }
            }

            return result;
        }

        // The 96-vertex orbit: every EVEN arrangement of (φ/2, 1/2, 1/(2φ), 0) with all sign choices on the
        // three nonzero entries (index 3 is the zero slot, so it carries no sign).
        private static void AddEvenPermutationOrbit(List<Vector4> vertices)
        {
            var magnitudes = new[] { 0.5f * Phi, 0.5f, 0.5f / Phi, 0f };
            foreach (var permutation in EvenPermutations4)
            {
                for (var signs = 0; signs < 8; signs++)
                {
                    var v = Vector4.zero;
                    var bit = 0;
                    for (var i = 0; i < 4; i++)
                    {
                        var value = magnitudes[permutation[i]];
                        if (permutation[i] != 3)
                        {
                            value = (signs >> bit & 1) == 0 ? value : -value;
                            bit++;
                        }

                        v[i] = value;
                    }

                    vertices.Add(v);
                }
            }
        }

        // Removes two completely orthogonal great decagons (the grand antiprism's missing rings). The rings are
        // NOT axis-aligned in icosian coordinates, so ring A is traced combinatorially from any edge; ring B from
        // an edge whose two vertices are both orthogonal to ring A's plane. Exactly 100 vertices survive.
        private static Vector4[] RemoveOrthogonalDecagons(Vector4[] cell)
        {
            var adjacency = EdgeGraph4(cell, 0.5f * Phi);
            RequireUniformDegree(adjacency, 12, "600-cell");

            var ringA = TraceGreatDecagon(cell, 0, adjacency[0][0]);
            FindOrthogonalEdge(cell, adjacency, cell[ringA[0]], cell[ringA[1]], out var first, out var second);
            var ringB = TraceGreatDecagon(cell, first, second);

            foreach (var a in ringA)
            {
                foreach (var b in ringB)
                {
                    Require(Mathf.Abs(Vector4.Dot(cell[a], cell[b])) < 1e-4f,
                        "the decagon rings must be completely orthogonal");
                }
            }

            var removed = new HashSet<int>(ringA);
            removed.UnionWith(ringB);
            Require(removed.Count == 20, "the two decagons must remove 20 distinct vertices");

            var survivors = new List<Vector4>(cell.Length - removed.Count);
            for (var i = 0; i < cell.Length; i++)
            {
                if (!removed.Contains(i))
                {
                    survivors.Add(cell[i]);
                }
            }

            Require(survivors.Count == 100, "the grand antiprism must have exactly 100 vertices");
            return survivors.ToArray();
        }

        // v_{k+1} = φ·v_k − v_{k−1} (the three-term recurrence with 2·cos 36° = φ) walks the great decagon
        // through an edge of the 600-cell: every iterate must land back on a vertex and close after ten steps.
        private static int[] TraceGreatDecagon(Vector4[] vertices, int first, int second)
        {
            var ring = new int[10];
            ring[0] = first;
            ring[1] = second;
            for (var k = 2; k < 10; k++)
            {
                ring[k] = SnapToVertex(vertices, Phi * vertices[ring[k - 1]] - vertices[ring[k - 2]]);
                Require(ring[k] != -1, "the decagon recurrence left the vertex set");
            }

            Require(new HashSet<int>(ring).Count == 10, "a great decagon must visit ten distinct vertices");
            var closing = SnapToVertex(vertices, Phi * vertices[ring[9]] - vertices[ring[8]]);
            Require(closing == first, "the great decagon must close after ten steps");
            return ring;
        }

        // An edge (dot = φ/2 pair) both of whose vertices are orthogonal to ring A's plane — the seed of the
        // completely orthogonal decagon.
        private static void FindOrthogonalEdge(
            Vector4[] cell, List<int>[] adjacency, Vector4 planeA, Vector4 planeB, out int first, out int second)
        {
            for (var i = 0; i < cell.Length; i++)
            {
                if (!OrthogonalToPlane(cell[i], planeA, planeB))
                {
                    continue;
                }

                var neighbours = adjacency[i];
                for (var n = 0; n < neighbours.Count; n++)
                {
                    if (OrthogonalToPlane(cell[neighbours[n]], planeA, planeB))
                    {
                        first = i;
                        second = neighbours[n];
                        return;
                    }
                }
            }

            throw new InvalidOperationException("ShapeCatalog: no edge orthogonal to the first decagon's plane");
        }

        private static bool OrthogonalToPlane(Vector4 v, Vector4 planeA, Vector4 planeB)
        {
            return Mathf.Abs(Vector4.Dot(v, planeA)) < 1e-4f && Mathf.Abs(Vector4.Dot(v, planeB)) < 1e-4f;
        }

        private static int SnapToVertex(Vector4[] vertices, Vector4 target)
        {
            for (var i = 0; i < vertices.Length; i++)
            {
                if ((vertices[i] - target).sqrMagnitude < 1e-6f)
                {
                    return i;
                }
            }

            return -1;
        }

        // Perspective projection from the w axis, p3 = (x, y, z) / (c − w): nearer-in-w shells project larger,
        // giving the layered-shell look. c starts at 2 (every |w| ≤ φ/2, far from the pole); if two projected
        // vertices ever collided, c nudges outward and retries. Build() then normalizes the result.
        private static Vector3[] ProjectPerspectiveFromW(Vector4[] vertices)
        {
            var c = 2f;
            for (var attempt = 0; attempt < 8; attempt++)
            {
                var projected = new Vector3[vertices.Length];
                for (var i = 0; i < vertices.Length; i++)
                {
                    var inverse = 1f / (c - vertices[i].w);
                    projected[i] = new Vector3(vertices[i].x * inverse, vertices[i].y * inverse, vertices[i].z * inverse);
                }

                if (AllDistinct(projected))
                {
                    return projected;
                }

                c += 0.25f;
            }

            throw new InvalidOperationException("ShapeCatalog: no collision-free perspective constant found");
        }

        private static bool AllDistinct(Vector3[] points)
        {
            for (var i = 0; i < points.Length; i++)
            {
                for (var j = i + 1; j < points.Length; j++)
                {
                    if ((points[i] - points[j]).sqrMagnitude < 1e-8f)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static List<int>[] EdgeGraph4(Vector4[] vertices, float edgeDot)
        {
            var adjacency = new List<int>[vertices.Length];
            for (var i = 0; i < vertices.Length; i++)
            {
                adjacency[i] = new List<int>();
            }

            for (var i = 0; i < vertices.Length; i++)
            {
                for (var j = i + 1; j < vertices.Length; j++)
                {
                    if (Mathf.Abs(Vector4.Dot(vertices[i], vertices[j]) - edgeDot) < 1e-4f)
                    {
                        adjacency[i].Add(j);
                        adjacency[j].Add(i);
                    }
                }
            }

            return adjacency;
        }

        private static void RequireUniformDegree(List<int>[] adjacency, int degree, string label)
        {
            for (var i = 0; i < adjacency.Length; i++)
            {
                Require(adjacency[i].Count == degree, $"{label}: every vertex must have degree {degree}");
            }
        }

        private static int CountEdges(List<int>[] adjacency)
        {
            var total = 0;
            for (var i = 0; i < adjacency.Length; i++)
            {
                total += adjacency[i].Count;
            }

            return total / 2;
        }

        // The catalog is deterministic build-time data: a violated invariant is an authoring bug, so fail loudly
        // at static-construction time rather than shipping a malformed shape.
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException("ShapeCatalog: " + message);
            }
        }

        private static Vector3 Cyclic(float x, float y, float z, int shift)
        {
            return shift switch
            {
                1 => new Vector3(z, x, y),
                2 => new Vector3(y, z, x),
                _ => new Vector3(x, y, z),
            };
        }

        private static Vector3[] Normalized(IReadOnlyList<Vector3> vertices)
        {
            var result = new Vector3[vertices.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = vertices[i].normalized;
            }

            return result;
        }

        private static float Min(IReadOnlyList<float> values)
        {
            var min = float.MaxValue;
            for (var i = 0; i < values.Count; i++)
            {
                min = Mathf.Min(min, values[i]);
            }

            return min;
        }

        private static float Max(IReadOnlyList<float> values)
        {
            var max = float.MinValue;
            for (var i = 0; i < values.Count; i++)
            {
                max = Mathf.Max(max, values[i]);
            }

            return max;
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
    }
}
