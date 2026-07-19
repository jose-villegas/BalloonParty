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

        // Per-shape multiplier on the settings' spin speed — complex shapes read better turning slowly
        // (gravitas), simple ones can stay zippy; 1 = the global speed.
        internal readonly float SpinScale;

        // Per-shape multiplier on the settings' pen speed — a shape whose walks are much longer than its pen
        // count needs faster pens to keep the whole figure inked (the star ball's outlines); 1 = global.
        internal readonly float PenSpeedScale;

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
            int denomination, float radiusScale, Vector3[] vertices, FormationWalk[] walks,
            bool alignToHit = false, float spinScale = 1f, float penSpeedScale = 1f)
        {
            Denomination = denomination;
            RadiusScale = radiusScale;
            SpinScale = spinScale;
            PenSpeedScale = penSpeedScale;
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
    ///     edges); 10 is an octagonal bipyramid; 12 the hexagonal prism, 20 the dodecahedron, 30 an
    ///     ball of six outline stars on the octahedral axes, 50 a 10×5 torus grid, and 100 a spherical-spiral
    ///     yarn ball (one closed coil) — the top tier follows silhouette-over-density. The tables
    ///     are built once in the static constructor and returned by reference from <see cref="TryGet"/>, so a
    ///     lookup never allocates.
    ///
    ///     <see cref="Denominations"/> is the decomposition ladder (largest-first) the optimal coin-change split
    ///     (<see cref="BigScoreTrailBehaviour.Decompose"/>) draws its pieces from.
    /// </summary>
    internal static class ShapeCatalog
    {
        // φ (golden ratio) — the dodecahedron's vertex coordinates are framed on it.
        private const float Phi = 1.6180339887498949f;

        private static readonly int[] LadderDenominations = { 100, 50, 30, 20, 15, 12, 10, 9, 8, 7, 6, 5, 4, 3, 2 };

        private static readonly Dictionary<int, FormationShape> Shapes = BuildShapes();

        // The largest RadiusScale across every built shape — fixed for the process lifetime, so callers that
        // used to re-scan the catalog per pop (BigScoreTrailBehaviour.FitScale/Begin) can read it once instead.
        internal static readonly float MaxRadiusScale = ComputeMaxRadiusScale(Shapes);

        internal static IReadOnlyList<int> Denominations => LadderDenominations;

        internal static bool TryGet(int denomination, out FormationShape shape)
        {
            return Shapes.TryGetValue(denomination, out shape);
        }

        // Forces the one-time static table build at a moment of the caller's choosing (scope start, with the
        // other prewarms) instead of on the first big score of a session, mid-gameplay.
        internal static void Warm()
        {
            _ = Shapes.Count;
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
                { 7, BuildHexagonalPyramid() },
                { 8, BuildCube() },
                { 9, BuildTriangularCupola() },
                { 10, BuildOctagonalBipyramid() },
                { 12, BuildHexagonalPrism() },
                { 15, BuildPentagonalCupola() },
                { 20, BuildDodecahedron() },
                { 30, BuildStarBall() },
                { 50, BuildTorus() },
                { 100, BuildYarnBall() },
            };
        }

        private static float ComputeMaxRadiusScale(IReadOnlyDictionary<int, FormationShape> shapes)
        {
            var max = 0f;
            foreach (var shape in shapes.Values)
            {
                if (shape.RadiusScale > max)
                {
                    max = shape.RadiusScale;
                }
            }

            return max > 0f ? max : 1f;
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

        // 7 = a hexagonal pyramid: six base vertices ringed under one apex. Single-inked like the square
        // pyramid (5) — one weaving cycle threads every slant edge plus alternate base edges (passing
        // through the apex three times), three shuttles fill the remaining base edges. Apex degree 6,
        // base degree 3.
        private static FormationShape BuildHexagonalPyramid()
        {
            const int baseCount = 6;
            const int apex = baseCount;
            var vertices = new Vector3[baseCount + 1];
            for (var i = 0; i < baseCount; i++)
            {
                var azimuth = 2f * Mathf.PI * i / baseCount;
                vertices[i] = new Vector3(Mathf.Cos(azimuth) * 0.85f, Mathf.Sin(azimuth) * 0.85f, -0.4f);
            }

            vertices[apex] = new Vector3(0f, 0f, 0.95f);

            var walks = new[]
            {
                Chord(0, 1, apex, 2, 3, apex, 4, 5, apex),
                Chord(1, 2), Chord(3, 4), Chord(5, 0),
            };
            return Build(7, 0.9f, vertices, walks);
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

        // 9 = a triangular cupola (Johnson J3): a hexagon base and a triangle top joined by an alternating
        // band of three squares and three triangles — 6 + 3 vertices, all 15 edges equal. A bowl/frustum
        // silhouette, distinct from the pointy bipyramid at 10 (and a small sibling to the 15's pentagonal
        // cupola). The hexagon and triangle rings draw the caps; three triangle walks thread the six lateral
        // edges through each top vertex, retracing only the three triangle-base hexagon edges (the minimal
        // doubling the six odd-degree base vertices allow).
        private static FormationShape BuildTriangularCupola()
        {
            const float hexRadius = 1f;                // 1/(2 sin 30°), unit-edge hexagon
            const float triRadius = 0.5773503f;        // 1/(2 sin 60°), unit-edge triangle
            const float halfHeight = 0.4082483f;       // half the unit-edge cupola height

            var vertices = new Vector3[9];
            for (var j = 0; j < 6; j++)
            {
                var a = 2f * Mathf.PI * j / 6f;
                vertices[j] = new Vector3(Mathf.Cos(a) * hexRadius, Mathf.Sin(a) * hexRadius, -halfHeight);
            }

            for (var i = 0; i < 3; i++)
            {
                // Each top vertex sits above the midpoint of a hexagon edge — offset half a hexagon step.
                var a = 2f * Mathf.PI * i / 3f + Mathf.PI / 6f;
                vertices[6 + i] = new Vector3(Mathf.Cos(a) * triRadius, Mathf.Sin(a) * triRadius, halfHeight);
            }

            var walks = new[]
            {
                Chord(0, 1, 2, 3, 4, 5),
                Chord(6, 7, 8),
                Chord(6, 0, 1), Chord(7, 2, 3), Chord(8, 4, 5),
            };
            return Build(9, 1f, vertices, walks);
        }

        // 12 = hexagonal prism (superseded the small stellated dodecahedron: its twelve overlapping
        // pentagrams read as a tangle mid-tumble — the prism family scales legibly instead: 6 is the
        // triangular prism, 12 this). Two hexagon loops plus six vertical shuttles along the wide axis.
        private static FormationShape BuildHexagonalPrism()
        {
            var vertices = new Vector3[12];
            for (var i = 0; i < 6; i++)
            {
                vertices[i] = PolarAt(60f * i, -0.55f);
                vertices[6 + i] = PolarAt(60f * i, 0.55f);
            }

            var bottom = new[] { 0, 1, 2, 3, 4, 5 };
            var top = new[] { 6, 7, 8, 9, 10, 11 };
            var walks = new FormationWalk[8];
            walks[0] = Chord(bottom);
            walks[1] = Chord(top);
            for (var i = 0; i < 6; i++)
            {
                walks[2 + i] = Chord(i, 6 + i);
            }

            return Build(12, 1.1f, vertices, walks);
        }

        // 15 = a pentagonal cupola (Johnson J5): a decagon base and a pentagon top joined by an alternating
        // band of five squares and five triangles — 10 + 5 vertices, all 25 edges equal. The decagon and
        // pentagon rings draw the caps; five triangle walks thread the ten lateral edges through each pentagon
        // apex, retracing only the five triangle-base decagon edges (the minimal doubling the ten odd-degree
        // base vertices allow). Every square face still reads as an outline across the rings + laterals.
        private static FormationShape BuildPentagonalCupola()
        {
            const float decagonRadius = 1.6180340f;   // 1/(2 sin 18°), unit-edge decagon
            const float pentagonRadius = 0.8506508f;   // 1/(2 sin 36°), unit-edge pentagon
            const float halfHeight = 0.2628655f;       // half the unit-edge cupola height

            var vertices = new Vector3[15];
            for (var j = 0; j < 10; j++)
            {
                var a = 2f * Mathf.PI * j / 10f;
                vertices[j] = new Vector3(Mathf.Cos(a) * decagonRadius, Mathf.Sin(a) * decagonRadius, -halfHeight);
            }

            for (var i = 0; i < 5; i++)
            {
                // Each pentagon apex sits above the midpoint of a decagon edge — offset half a decagon step.
                var a = 2f * Mathf.PI * i / 5f + Mathf.PI / 10f;
                vertices[10 + i] = new Vector3(
                    Mathf.Cos(a) * pentagonRadius, Mathf.Sin(a) * pentagonRadius, halfHeight);
            }

            var walks = new[]
            {
                Chord(0, 1, 2, 3, 4, 5, 6, 7, 8, 9),
                Chord(10, 11, 12, 13, 14),
                Chord(10, 0, 1), Chord(11, 2, 3), Chord(12, 4, 5), Chord(13, 6, 7), Chord(14, 8, 9),
            };
            return Build(15, 1.35f, vertices, walks);
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

        // 30 = a GARLAND of outline stars: three big 5-pointed stars traced by their silhouette — tip,
        // notch, tip, notch — never crossing their own interior (chord-drawn stars read as tangles: the
        // pen slices through the star instead of drawing the shape you would cut from paper). Their caps
        // sit 120° apart around the equator, threaded into ONE closed walk by straight tip-to-tip SEAMS:
        // every pen draws a star, exits along the seam into the next star, and keeps flowing around the
        // band — the seam IS the migration between stars (a time-rotating "hop" frame was tried first and
        // smeared into spirals: pens kept orbiting while the frame turned). After closing an outline the
        // pen re-walks two segments from the entry tip back to the exit tip, so no seam ever cuts across
        // a star's interior; those two edges per star are double-inked (the 12/20 precedent). Half-speed
        // SpinScale for readability.
        // 30 = a BALL OF STARS: six five-point OUTLINE stars stamped onto the sphere at the six octahedral
        // directions (±x, ±y, ±z). Six stars spread evenly over the WHOLE ball (the coverage three coplanar
        // stars never reached), and each is drawn by its SILHOUETTE — the pen orbits the tip-notch-tip outline
        // and never crosses the interior (a {5/2} pentagram's crossing lines read as a tangle; the outline is
        // the shape you'd cut). Each outline is 10 points (5 golden-notch tips), so this is the catalog's one
        // shape where the path (60 vertices) is richer than the pen count: the denomination's 30 pens are
        // distributed five per outline and orbit it, tiling the 10-segment silhouette. Points are SPHERIZED
        // (unit vectors) and the segments are arcs that hug the surface. Faster pens (PenSpeedScale) keep the
        // longer outlines fully inked; half-speed SpinScale; tipCapRadians is the coverage dial.
        private static FormationShape BuildStarBall()
        {
            const int starCount = 6;
            const int tipCount = 5;
            const int outlineCount = 2 * tipCount;
            const float tipCapRadians = 0.7f;

            // Golden-ratio notch: inner/outer tangent radius ≈ 0.382, the classic five-point star cut.
            var notchCapRadians = Mathf.Asin(0.382f * Mathf.Sin(tipCapRadians));

            var axes = new[]
            {
                Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
            };
            var vertices = new Vector3[starCount * outlineCount];
            var walks = new FormationWalk[starCount];
            for (var f = 0; f < starCount; f++)
            {
                var normal = axes[f];
                var reference = Vector3.Cross(
                    normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                var binormal = Vector3.Cross(normal, reference);
                var star = f * outlineCount;
                var loop = new int[outlineCount];
                for (var i = 0; i < outlineCount; i++)
                {
                    // Even outline points are tips, odd ones the notches between them.
                    var cap = i % 2 == 0 ? tipCapRadians : notchCapRadians;
                    var angle = Mathf.PI * i / tipCount;
                    vertices[star + i] = Mathf.Cos(cap) * normal
                        + Mathf.Sin(cap) * (Mathf.Cos(angle) * reference + Mathf.Sin(angle) * binormal);
                    loop[i] = star + i;
                }

                // The silhouette in order — tip, notch, tip … — traced as arcs that hug the surface.
                walks[f] = new FormationWalk(loop, arc: true);
            }

            return Build(30, 1.3f, vertices, walks, spinScale: 0.5f, penSpeedScale: 1.6f);
        }

        // 50 = a 10x5 torus grid — the crown tier needed a SILHOUETTE, not more line density: nothing
        // else in the catalog has a hole, so the doughnut reads instantly under tumble where the star
        // duals blurred together (supersedes the rhombicosacron). The walks are the grid's own rings —
        // 5 major decagons + 10 minor pentagons — every edge in exactly one ring (single-inked) and
        // every vertex degree 4.
        private static FormationShape BuildTorus()
        {
            const int majorCount = 10;
            const int minorCount = 5;
            const float minorRadius = 0.45f;

            var vertices = new Vector3[majorCount * minorCount];
            for (var i = 0; i < majorCount; i++)
            {
                var theta = 2f * Mathf.PI * i / majorCount;
                var cosTheta = Mathf.Cos(theta);
                var sinTheta = Mathf.Sin(theta);
                for (var j = 0; j < minorCount; j++)
                {
                    var psi = 2f * Mathf.PI * j / minorCount;
                    var ring = 1f + minorRadius * Mathf.Cos(psi);
                    vertices[i * minorCount + j] =
                        new Vector3(ring * cosTheta, ring * sinTheta, minorRadius * Mathf.Sin(psi));
                }
            }

            var walks = new FormationWalk[minorCount + majorCount];
            for (var j = 0; j < minorCount; j++)
            {
                var loop = new int[majorCount];
                for (var i = 0; i < majorCount; i++)
                {
                    loop[i] = i * minorCount + j;
                }

                walks[j] = Chord(loop);
            }

            for (var i = 0; i < majorCount; i++)
            {
                var loop = new int[minorCount];
                for (var j = 0; j < minorCount; j++)
                {
                    loop[j] = i * minorCount + j;
                }

                walks[minorCount + i] = Chord(loop);
            }

            return Build(50, 1.45f, vertices, walks);
        }

        // 100 = a spherical spiral "yarn ball" (superseded the grand antiprism: 500 projected 4D edges read
        // as noise mid-tumble — the crown wants a SILHOUETTE). One closed 100-vertex walk: the polar angle
        // runs pole-to-pole and back as a triangle wave while the azimuth advances nine full turns (coprime
        // with the 100 samples, so the half-sample crossing guard holds), interleaving ~18 windings — dense enough to read as a SOLID wound ball.
        // Arc interpolation (the vertices are all on the unit sphere) keeps every winding a great-circle
        // segment, so the higher turn count costs no polygonal faceting between the 100 samples.
        // Half-sample offsets make the two coils provably never coincide at the crossings. All 100 pens
        // chase one continuous line.
        private static FormationShape BuildYarnBall()
        {
            const int count = 100;
            const float turns = 9f;
            const float polarPad = 0.12f;

            var vertices = new Vector3[count];
            var loop = new int[count];
            for (var i = 0; i < count; i++)
            {
                var s = (i + 0.5f) / count;
                var wave = s < 0.5f ? 2f * s : 2f - 2f * s;
                var polar = polarPad + (Mathf.PI - 2f * polarPad) * wave;
                var azimuth = turns * 2f * Mathf.PI * s;
                var sinPolar = Mathf.Sin(polar);
                vertices[i] = new Vector3(
                    sinPolar * Mathf.Cos(azimuth), sinPolar * Mathf.Sin(azimuth), Mathf.Cos(polar));
                loop[i] = i;
            }

            return Build(100, 1.6f, vertices, new[] { new FormationWalk(loop, arc: true) });
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
            int denomination, float radiusScale, Vector3[] vertices, FormationWalk[] walks,
            bool alignToHit = false, float spinScale = 1f, float penSpeedScale = 1f)
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

            return new FormationShape(
                denomination, radiusScale, vertices, walks, alignToHit, spinScale, penSpeedScale);
        }
    }
}
