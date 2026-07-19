using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Game.Score.Behaviours;
using BalloonParty.Shared.Messages;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ScoreTrailBehaviourResolverTests
    {
        private const string Red = "Red";

        [Test]
        public void Resolve_EmptyConfig_ReturnsDefaultHandler()
        {
            var handler = new SpyBehaviour();
            var resolver = BuildResolver(handler, new FakeConfig());

            Assert.AreSame(handler, resolver.Resolve(1));
            Assert.AreSame(handler, resolver.Resolve(1000));
        }

        [Test]
        public void Resolve_NullConfig_ReturnsDefaultHandler()
        {
            var handler = new SpyBehaviour();
            var resolver = BuildResolver(handler, config: null);

            Assert.AreSame(handler, resolver.Resolve(5));
        }

        [Test]
        public void Resolve_PointsBelowLowestThreshold_FallsBackToDefault()
        {
            var handler = new SpyBehaviour();
            var config = new FakeConfig(new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 7));
            var resolver = BuildResolver(handler, config);

            // Below the only entry's threshold no entry clears, so the default carrier answers; at/above it the
            // matching entry answers (the same instance until a second behaviour id lands).
            Assert.AreSame(handler, resolver.Resolve(3));
            Assert.AreSame(handler, resolver.Resolve(7));
        }

        [Test]
        public void Resolve_RoutesByGroupTotal_BigScoreClaimsAboveThreshold()
        {
            var defaultHandler = new SpyBehaviour();
            var bigHandler = new SpyBehaviour();
            var config = new FakeConfig(
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.BigScore, 7),
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 0));
            var handlers = new Dictionary<ScoreTrailBehaviourId, IScoreTrailBehaviour>
            {
                { ScoreTrailBehaviourId.DefaultScore, defaultHandler },
                { ScoreTrailBehaviourId.BigScore, bigHandler },
            };
            var resolver = new ScoreTrailBehaviourResolver(config, handlers);

            Assert.AreSame(defaultHandler, resolver.Resolve(6));
            Assert.AreSame(bigHandler, resolver.Resolve(7));
            Assert.AreSame(bigHandler, resolver.Resolve(500));
        }

        [Test]
        public void PrincipalIdFor_DelegatesToResolvedHandler()
        {
            var handler = new SpyBehaviour();
            var resolver = BuildResolver(
                handler, new FakeConfig(new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 0)));
            var msg = new ScorePointsGroupMessage(Red, Vector3.zero, points: 3, lastScore: 7, multiplier: 1, hitDirection: Vector3.zero);

            var id = resolver.PrincipalIdFor(msg);

            Assert.AreEqual(new TrailId(Red, msg.FirstScore), id);
            Assert.AreEqual(1, handler.PrincipalCalls);
        }

        [Test]
        public void Decompose_OptimalCoinChange_MatchesDesignExamples()
        {
            // Fewest pieces, remainder-free splits preferred, reconstructed largest-first. 13 = 10 + 3 (two
            // pieces), NOT 12 + 1 (which a greedy pass over a 12-inclusive ladder would produce).
            CollectionAssert.AreEqual(new[] { 10, 3 }, Decompose(13));

            // 12 is now a denomination (the small stellated dodecahedron): 12 = [12], and 14 = 12 + 2.
            CollectionAssert.AreEqual(new[] { 12 }, Decompose(12));
            CollectionAssert.AreEqual(new[] { 12, 2 }, Decompose(14));

            // 7, 9 and 15 are now denominations (hexagonal pyramid, triaugmented triangular prism, pentagonal
            // cupola): each is a single piece rather than a multi-shape split.
            CollectionAssert.AreEqual(new[] { 7 }, Decompose(7));
            CollectionAssert.AreEqual(new[] { 9 }, Decompose(9));
            CollectionAssert.AreEqual(new[] { 15 }, Decompose(15));

            // The 50 (rhombicosacron) and its showcase combo are unchanged by the 100's arrival.
            CollectionAssert.AreEqual(new[] { 50 }, Decompose(50));
            CollectionAssert.AreEqual(new[] { 50, 12 }, Decompose(62));

            // 100 (the grand antiprism) is the ladder's crown: 100 = [100] (no longer 50 + 50), 112 = 100 + 12,
            // 200 = two crowns, 250 = 100 + 100 + 50 (three pieces beats five 50s).
            CollectionAssert.AreEqual(new[] { 100 }, Decompose(100));
            CollectionAssert.AreEqual(new[] { 100, 12 }, Decompose(112));
            CollectionAssert.AreEqual(new[] { 100, 100 }, Decompose(200));
            CollectionAssert.AreEqual(new[] { 100, 100, 50 }, Decompose(250));

            // 2 = the line shape; 1 = no shape, just a single default trail (the terminal remainder).
            CollectionAssert.AreEqual(new[] { 2 }, Decompose(2));
            CollectionAssert.AreEqual(new[] { 1 }, Decompose(1));
        }

        [Test]
        public void Decompose_AlwaysSumsToTotalAndDescends()
        {
            var result = new List<int>();
            for (var total = 2; total <= 300; total++)
            {
                BigScoreTrailBehaviour.Decompose(total, result);

                var sum = 0;
                for (var i = 0; i < result.Count; i++)
                {
                    sum += result[i];
                    if (i > 0)
                    {
                        Assert.LessOrEqual(result[i], result[i - 1], $"{total} not descending");
                    }
                }

                Assert.AreEqual(total, sum, $"{total} did not sum to itself");
            }
        }

        [Test]
        public void ShapeCatalog_EveryLadderDenomination_HasAConsistentShape()
        {
            foreach (var denomination in ShapeCatalog.Denominations)
            {
                Assert.IsTrue(ShapeCatalog.TryGet(denomination, out var shape), $"missing shape {denomination}");
                Assert.AreEqual(denomination, shape.Denomination);
                Assert.AreEqual(denomination, shape.Vertices.Length, $"{denomination} vertex count");

                var pens = 0;
                foreach (var count in shape.PensPerWalk)
                {
                    pens += count;
                }

                Assert.AreEqual(denomination, pens, $"{denomination} pens must equal its vertex count");
                Assert.AreEqual(shape.Walks.Length, shape.PensPerWalk.Length);
            }
        }

        [Test]
        public void ShapeCatalog_NonDenominationLookup_Fails()
        {
            // 7 is not a denomination (the optimal split reaches it as 5 + 2), so no shape is authored for it.
            Assert.IsFalse(ShapeCatalog.TryGet(7, out _));
        }

        [Test]
        public void ShapeCatalog_HexagonalPrism_TwoHexagonsAndSixShuttles()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(12, out var shape));

            Assert.AreEqual(12, shape.Vertices.Length);
            Assert.AreEqual(12, new HashSet<Vector3>(shape.Vertices).Count, "twelve distinct vertices");
            Assert.AreEqual(8, shape.Walks.Length, "two hexagon loops + six vertical shuttles");

            var edges = InspectCircuits(shape);
            Assert.AreEqual(18, edges.Multiplicity.Count, "a hexagonal prism has 18 edges");
            CollectionAssert.AreEqual(
                new[] { 1 }, DistinctValues(edges.Multiplicity), "every edge inked exactly once (single-inked)");
            Assert.AreEqual(12, edges.Touched.Count, "all twelve vertices covered");

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(12, CountByDegree(degrees, 3), "every prism vertex has degree three");
        }

        [Test]
        public void ShapeCatalog_Dodecahedron_TwelveDoubleInkedPentagonFaces()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(20, out var shape));
            Assert.AreEqual(12, shape.Walks.Length, "twelve pentagon face circuits");
            AssertFiveDistinctPerWalk(shape);

            var edges = InspectCircuits(shape);
            Assert.Less(edges.MaxLength - edges.MinLength, 1e-3f, "all pentagon edges are the same length");
            Assert.AreEqual(30, edges.Multiplicity.Count, "a dodecahedron has 30 edges");
            CollectionAssert.AreEqual(new[] { 2 }, DistinctValues(edges.Multiplicity), "each edge shared by two faces");
            Assert.AreEqual(20, edges.Touched.Count, "all twenty vertices covered");
        }

        [Test]
        public void ShapeCatalog_StarBall_GarlandOfThreeSeamThreadedOutlineStars()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(30, out var shape));

            Assert.AreEqual(30, shape.Vertices.Length);
            Assert.AreEqual(30, new HashSet<Vector3>(shape.Vertices).Count, "thirty distinct vertices");
            Assert.AreEqual(1, shape.Walks.Length, "one closed walk threads all three stars via seams");
            CollectionAssert.AreEqual(new[] { 30 }, shape.PensPerWalk, "every pen flows the whole garland");
            Assert.AreEqual(39, shape.Walks[0].Vertices.Length,
                "three 10-point outlines + two re-walked steps and a seam entry each");

            var edges = InspectCircuits(shape);
            Assert.AreEqual(33, edges.Multiplicity.Count, "thirty outline edges plus three seams");
            CollectionAssert.AreEqual(
                new[] { 1, 2 }, DistinctValues(edges.Multiplicity),
                "single-inked outlines and seams; only the entry-to-exit re-walk is doubled");
            Assert.AreEqual(30, edges.Touched.Count, "all vertices covered");

            var doubled = 0;
            foreach (var multiplicity in edges.Multiplicity.Values)
            {
                if (multiplicity == 2)
                {
                    doubled++;
                }
            }

            Assert.AreEqual(6, doubled, "exactly two re-walked segments per star");
        }

        [Test]
        public void ShapeCatalog_OctagonalBipyramid_SingleInkedTwoWalks()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(10, out var shape));

            Assert.AreEqual(10, shape.Vertices.Length);
            Assert.AreEqual(10, new HashSet<Vector3>(shape.Vertices).Count, "ten distinct vertices");
            Assert.AreEqual(2, shape.Walks.Length, "equator octagon + one pole-to-pole zigzag");

            var edges = InspectCircuits(shape);
            Assert.AreEqual(24, edges.Multiplicity.Count, "a bipyramid over an octagon has 24 edges");
            CollectionAssert.AreEqual(
                new[] { 1 }, DistinctValues(edges.Multiplicity), "every edge inked exactly once (single-inked)");
            Assert.AreEqual(10, edges.Touched.Count, "all ten vertices covered");

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(8, CountByDegree(degrees, 4), "eight equator vertices of degree four");
            Assert.AreEqual(2, CountByDegree(degrees, 8), "two apexes of degree eight");
        }

        [Test]
        public void ShapeCatalog_HexagonalPyramid_ApexHubOverHexagonBase()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(7, out var shape));

            Assert.AreEqual(7, shape.Vertices.Length);
            Assert.AreEqual(7, new HashSet<Vector3>(shape.Vertices).Count, "seven distinct vertices");
            Assert.AreEqual(4, shape.Walks.Length, "one weaving cycle + three base-edge shuttles");

            var edges = InspectCircuits(shape);
            Assert.AreEqual(12, edges.Multiplicity.Count, "six base edges + six slant edges");
            Assert.AreEqual(7, edges.Touched.Count, "all seven vertices covered");

            // Six odd-degree base vertices forbid a pure single-inked cover (like the square pyramid): the
            // three base shuttles are the minimal retraced edges, so multiplicities are 1s and 2s.
            CollectionAssert.AreEqual(new[] { 1, 2 }, DistinctValues(edges.Multiplicity));

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(1, CountByDegree(degrees, 6), "one apex hub joined to all six base vertices");
        }

        [Test]
        public void ShapeCatalog_TriaugmentedTriangularPrism_DeltahedronThreeApexes()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(9, out var shape));

            Assert.AreEqual(9, shape.Vertices.Length);
            Assert.AreEqual(9, new HashSet<Vector3>(shape.Vertices).Count, "nine distinct vertices");
            Assert.AreEqual(5, shape.Walks.Length, "two triangle rings + three apex diamonds");

            var edges = InspectCircuits(shape);
            Assert.Less(edges.MaxLength - edges.MinLength, 1e-3f, "a deltahedron — every edge the same length");
            Assert.AreEqual(21, edges.Multiplicity.Count, "a triaugmented triangular prism has 21 edges");
            Assert.AreEqual(9, edges.Touched.Count, "all nine vertices covered");

            // Six odd-degree prism vertices force minimal retracing: only the three verticals are doubled.
            CollectionAssert.AreEqual(new[] { 1, 2 }, DistinctValues(edges.Multiplicity));

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(3, CountByDegree(degrees, 4), "three augmentation apexes of degree four");
        }

        [Test]
        public void ShapeCatalog_PentagonalCupola_DecagonAndPentagonCaps()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(15, out var shape));

            Assert.AreEqual(15, shape.Vertices.Length);
            Assert.AreEqual(15, new HashSet<Vector3>(shape.Vertices).Count, "fifteen distinct vertices");
            Assert.AreEqual(7, shape.Walks.Length, "decagon + pentagon + five triangle facets");

            var edges = InspectCircuits(shape);
            Assert.Less(edges.MaxLength - edges.MinLength, 1e-3f, "a Johnson solid — every edge the same length");
            Assert.AreEqual(25, edges.Multiplicity.Count, "a pentagonal cupola has 25 edges");
            Assert.AreEqual(15, edges.Touched.Count, "all fifteen vertices covered");

            // Ten odd-degree decagon vertices force minimal retracing: the five triangle-base edges are doubled.
            CollectionAssert.AreEqual(new[] { 1, 2 }, DistinctValues(edges.Multiplicity));

            var doubled = 0;
            foreach (var multiplicity in edges.Multiplicity.Values)
            {
                if (multiplicity == 2)
                {
                    doubled++;
                }
            }

            Assert.AreEqual(5, doubled, "one retraced base per triangle facet");
        }

        [Test]
        public void ShapeCatalog_Torus_GridRingsSingleInked()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(50, out var shape));

            Assert.AreEqual(50, shape.Vertices.Length);
            Assert.AreEqual(50, new HashSet<Vector3>(shape.Vertices).Count, "fifty distinct vertices");
            Assert.AreEqual(15, shape.Walks.Length, "five major decagons + ten minor pentagons");

            var edges = InspectCircuits(shape);
            Assert.AreEqual(100, edges.Multiplicity.Count, "a 10x5 torus grid has 100 edges");
            CollectionAssert.AreEqual(
                new[] { 1 }, DistinctValues(edges.Multiplicity), "every edge inked exactly once (single-inked)");
            Assert.AreEqual(50, edges.Touched.Count, "all fifty vertices covered");

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(50, CountByDegree(degrees, 4), "every torus-grid vertex has degree four");
        }

        [Test]
        public void ShapeCatalog_YarnBall_OneClosedCoil()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(100, out var shape));

            Assert.AreEqual(100, shape.Vertices.Length);
            Assert.AreEqual(100, new HashSet<Vector3>(shape.Vertices).Count, "one hundred distinct vertices");
            Assert.AreEqual(1, shape.Walks.Length, "a single continuous coil");
            CollectionAssert.AreEqual(new[] { 100 }, shape.PensPerWalk, "all pens chase the one line");

            var edges = InspectCircuits(shape);
            Assert.AreEqual(100, edges.Multiplicity.Count, "a 100-vertex simple cycle has 100 edges");
            CollectionAssert.AreEqual(
                new[] { 1 }, DistinctValues(edges.Multiplicity), "every edge inked exactly once");
            Assert.AreEqual(100, edges.Touched.Count, "all vertices covered");

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(100, CountByDegree(degrees, 2), "a simple cycle: degree two everywhere");
        }

        private static void AssertFiveDistinctPerWalk(FormationShape shape)
        {
            foreach (var walk in shape.Walks)
            {
                Assert.AreEqual(5, walk.Vertices.Length, "a face circuit has five vertices");
                Assert.AreEqual(5, new HashSet<int>(walk.Vertices).Count, "a face circuit visits five distinct vertices");
            }
        }

        private static CircuitInspection InspectCircuits(FormationShape shape)
        {
            var multiplicity = new Dictionary<long, int>();
            var touched = new HashSet<int>();
            var minLength = float.MaxValue;
            var maxLength = float.MinValue;
            foreach (var walk in shape.Walks)
            {
                var ring = walk.Vertices;
                for (var i = 0; i < ring.Length; i++)
                {
                    var a = ring[i];
                    var b = ring[(i + 1) % ring.Length];
                    touched.Add(a);
                    var length = (shape.Vertices[a] - shape.Vertices[b]).magnitude;
                    minLength = Mathf.Min(minLength, length);
                    maxLength = Mathf.Max(maxLength, length);
                    var key = a < b ? (long)a * 100 + b : (long)b * 100 + a;
                    multiplicity.TryGetValue(key, out var count);
                    multiplicity[key] = count + 1;
                }
            }

            return new CircuitInspection(multiplicity, touched, minLength, maxLength);
        }

        private static int[] DistinctValues(Dictionary<long, int> multiplicity)
        {
            var set = new SortedSet<int>(multiplicity.Values);
            var values = new int[set.Count];
            set.CopyTo(values);
            return values;
        }

        private static Dictionary<int, int> DegreeHistogram(Dictionary<long, int> multiplicity)
        {
            var degree = new Dictionary<int, int>();
            foreach (var key in multiplicity.Keys)
            {
                var a = (int)(key / 100);
                var b = (int)(key % 100);
                degree.TryGetValue(a, out var da);
                degree[a] = da + 1;
                degree.TryGetValue(b, out var db);
                degree[b] = db + 1;
            }

            return degree;
        }

        private static int CountByDegree(Dictionary<int, int> degrees, int target)
        {
            var count = 0;
            foreach (var d in degrees.Values)
            {
                if (d == target)
                {
                    count++;
                }
            }

            return count;
        }

        private static int[] Decompose(int total)
        {
            var result = new List<int>();
            BigScoreTrailBehaviour.Decompose(total, result);
            return result.ToArray();
        }

        private static ScoreTrailBehaviourResolver BuildResolver(
            SpyBehaviour handler, IScoreTrailBehaviourConfiguration config)
        {
            var handlers = new Dictionary<ScoreTrailBehaviourId, IScoreTrailBehaviour>
            {
                { ScoreTrailBehaviourId.DefaultScore, handler },
            };
            return new ScoreTrailBehaviourResolver(config, handlers);
        }

        private readonly struct CircuitInspection
        {
            public readonly Dictionary<long, int> Multiplicity;
            public readonly HashSet<int> Touched;
            public readonly float MinLength;
            public readonly float MaxLength;

            public CircuitInspection(
                Dictionary<long, int> multiplicity, HashSet<int> touched, float minLength, float maxLength)
            {
                Multiplicity = multiplicity;
                Touched = touched;
                MinLength = minLength;
                MaxLength = maxLength;
            }
        }

        private sealed class SpyBehaviour : IScoreTrailBehaviour
        {
            public int PrincipalCalls { get; private set; }

            public TrailId GetPrincipalId(in ScorePointsGroupMessage msg)
            {
                PrincipalCalls++;
                return new TrailId(msg.ColorName, msg.FirstScore);
            }

            public void Begin(in ScoreTrailContext context)
            {
            }
        }

        private sealed class FakeConfig : IScoreTrailBehaviourConfiguration
        {
            private readonly ScoreTrailBehaviourEntry[] _entries;

            public IReadOnlyList<ScoreTrailBehaviourEntry> Entries => _entries;
            public BigScoreFormationSettings BigScoreSettings { get; }

            public FakeConfig(params ScoreTrailBehaviourEntry[] entries)
            {
                _entries = entries;
            }
        }
    }
}
