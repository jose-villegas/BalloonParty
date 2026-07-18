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
        public void Decompose_GreedyLargestFirst_MatchesDesignExamples()
        {
            // 13 = 10-sphere + triangle; the 12-sphere is deliberately absent from the ladder (12 would greedily
            // split 13 as 12+1, contradicting this example).
            CollectionAssert.AreEqual(new[] { 10, 3 }, Decompose(13));

            // 7 = triangular prism + one leftover default trail (a terminal remainder of 1).
            CollectionAssert.AreEqual(new[] { 6, 1 }, Decompose(7));

            // 250 = eight 30-spheres + one 10-sphere.
            CollectionAssert.AreEqual(new[] { 30, 30, 30, 30, 30, 30, 30, 30, 10 }, Decompose(250));

            // 2 = the line shape; 1 = no shape, just a single default trail.
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
        public void ShapeCatalog_DroppedAndNonDenominationLookups_Fail()
        {
            // 12 is authored nowhere (dropped from the ladder to honour 13 = 10+3); 7 is not a denomination.
            Assert.IsFalse(ShapeCatalog.TryGet(12, out _));
            Assert.IsFalse(ShapeCatalog.TryGet(7, out _));
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
