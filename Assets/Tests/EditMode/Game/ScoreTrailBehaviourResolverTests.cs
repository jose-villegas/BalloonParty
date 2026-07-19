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

            // 7 = 5 + 2 (two pieces, remainder-free) — better than greedy's 6 + 1 remainder split.
            CollectionAssert.AreEqual(new[] { 5, 2 }, Decompose(7));

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
        public void ShapeCatalog_StellatedDodecahedron_TwelveUniformPentagramCircuits()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(12, out var shape));
            Assert.AreEqual(12, shape.Walks.Length, "one pentagram circuit per icosahedron vertex");

            var touched = new HashSet<int>();
            foreach (var walk in shape.Walks)
            {
                var ring = walk.Vertices;
                Assert.AreEqual(5, ring.Length, "each circuit is a five-vertex pentagram");
                Assert.AreEqual(5, new HashSet<int>(ring).Count, "a circuit visits five distinct vertices");
                touched.UnionWith(ring);

                // A regular pentagram: every skip-2 chord (consecutive ring entries) is the same length.
                var reference = (shape.Vertices[ring[1]] - shape.Vertices[ring[0]]).magnitude;
                for (var s = 0; s < ring.Length; s++)
                {
                    var chord = (shape.Vertices[ring[(s + 1) % ring.Length]] - shape.Vertices[ring[s]]).magnitude;
                    Assert.AreEqual(reference, chord, 1e-3f, "pentagram chords must be uniform");
                }
            }

            Assert.AreEqual(12, touched.Count, "the twelve circuits together cover all twelve vertices");

            foreach (var pens in shape.PensPerWalk)
            {
                Assert.AreEqual(1, pens, "one pen per circuit (twelve 1s summing to the denomination)");
            }
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
        public void ShapeCatalog_Dodecadodecahedron_PentagonsAndPentagramsDoubleInked()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(30, out var shape));
            Assert.AreEqual(24, shape.Walks.Length, "twelve pentagons + twelve pentagrams");
            AssertFiveDistinctPerWalk(shape);

            var edges = InspectCircuits(shape);
            Assert.Less(edges.MaxLength - edges.MinLength, 1e-3f, "uniform edge length across pentagons and pentagrams");
            Assert.AreEqual(60, edges.Multiplicity.Count, "a dodecadodecahedron has 60 edges");
            CollectionAssert.AreEqual(
                new[] { 2 }, DistinctValues(edges.Multiplicity), "each edge shared by one pentagon and one pentagram");
            Assert.AreEqual(30, edges.Touched.Count, "all thirty vertices covered");
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
        public void ShapeCatalog_Rhombicosacron_EulerianCircuitsInkEveryEdgeOnce()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(50, out var shape));

            var unit = 0;
            foreach (var v in shape.Vertices)
            {
                Assert.AreEqual(1f, v.magnitude, 1e-3f, "every vertex is on the unit sphere");
                unit++;
            }

            Assert.AreEqual(50, unit);
            Assert.AreEqual(50, new HashSet<Vector3>(shape.Vertices).Count, "fifty distinct vertices");

            foreach (var walk in shape.Walks)
            {
                Assert.GreaterOrEqual(walk.Vertices.Length, 3, "each circuit is a closed loop");
            }

            var edges = InspectCircuits(shape);
            Assert.AreEqual(120, edges.Multiplicity.Count, "a rhombicosacron has 120 edges");
            CollectionAssert.AreEqual(
                new[] { 1 }, DistinctValues(edges.Multiplicity), "every edge inked exactly once (single-inked)");
            Assert.AreEqual(50, edges.Touched.Count, "all fifty vertices covered");

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(30, CountByDegree(degrees, 4), "thirty two-fold vertices of degree four");
            Assert.AreEqual(20, CountByDegree(degrees, 6), "twenty three-fold vertices of degree six");
        }

        [Test]
        public void ShapeCatalog_GrandAntiprism_FiveHundredEdgesSingleInked()
        {
            Assert.IsTrue(ShapeCatalog.TryGet(100, out var shape));
            Assert.AreEqual(100, shape.Vertices.Length, "one hundred vertices");
            Assert.AreEqual(
                100, new HashSet<Vector3>(shape.Vertices).Count, "projection must keep all vertices distinct");

            // Spliced walks may revisit their splice vertex, but every walk stays a closed loop long enough for
            // DistributePens to seed it — no circuit is ever left unpenned (and thus undrawn).
            foreach (var walk in shape.Walks)
            {
                Assert.GreaterOrEqual(walk.Vertices.Length, 5, "every circuit long enough to earn a pen");
            }

            foreach (var pens in shape.PensPerWalk)
            {
                Assert.GreaterOrEqual(pens, 1, "every circuit gets at least one pen");
            }

            var edges = InspectCircuits(shape);
            Assert.AreEqual(500, edges.Multiplicity.Count, "a grand antiprism has 500 edges");
            CollectionAssert.AreEqual(
                new[] { 1 }, DistinctValues(edges.Multiplicity), "every edge inked exactly once (single-inked)");
            Assert.AreEqual(100, edges.Touched.Count, "all one hundred vertices covered");

            var degrees = DegreeHistogram(edges.Multiplicity);
            Assert.AreEqual(100, CountByDegree(degrees, 10), "degree ten at every vertex");
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
