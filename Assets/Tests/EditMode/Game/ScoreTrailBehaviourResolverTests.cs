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
            var config = new FakeConfig(new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 40));
            var resolver = BuildResolver(handler, config);

            // Below the only entry's threshold no entry clears, so the default carrier answers; at/above it
            // the matching entry answers (the same instance until a second behaviour id lands in step 3).
            Assert.AreSame(handler, resolver.Resolve(10));
            Assert.AreSame(handler, resolver.Resolve(40));
        }

        [Test]
        public void Resolve_RoutesByGroupTotal_BigScoreClaimsAboveThreshold()
        {
            var defaultHandler = new SpyBehaviour();
            var bigHandler = new SpyBehaviour();
            var config = new FakeConfig(
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.BigScore, 40),
                new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 0));
            var handlers = new Dictionary<ScoreTrailBehaviourId, IScoreTrailBehaviour>
            {
                { ScoreTrailBehaviourId.DefaultScore, defaultHandler },
                { ScoreTrailBehaviourId.BigScore, bigHandler },
            };
            var resolver = new ScoreTrailBehaviourResolver(config, handlers);

            Assert.AreSame(defaultHandler, resolver.Resolve(39));
            Assert.AreSame(bigHandler, resolver.Resolve(40));
            Assert.AreSame(bigHandler, resolver.Resolve(500));
        }

        [Test]
        public void SelectTier_PicksHighestMinPointsCleared()
        {
            var tiers = new[]
            {
                Tier(minPoints: 40, vertexCount: 3, skip: 1),
                Tier(minPoints: 80, vertexCount: 4, skip: 1),
                Tier(minPoints: 150, vertexCount: 5, skip: 2),
            };

            Assert.AreEqual(3, BigScoreTrailBehaviour.SelectTier(tiers, 40).VertexCount);
            Assert.AreEqual(3, BigScoreTrailBehaviour.SelectTier(tiers, 79).VertexCount);
            Assert.AreEqual(4, BigScoreTrailBehaviour.SelectTier(tiers, 80).VertexCount);
            Assert.AreEqual(4, BigScoreTrailBehaviour.SelectTier(tiers, 149).VertexCount);

            var pentagram = BigScoreTrailBehaviour.SelectTier(tiers, 150);
            Assert.AreEqual(5, pentagram.VertexCount);
            Assert.AreEqual(2, pentagram.Skip);
        }

        [Test]
        public void SelectTier_BelowLowestThreshold_FallsBackToLowestTier()
        {
            var tiers = new[]
            {
                Tier(minPoints: 80, vertexCount: 4, skip: 1),
                Tier(minPoints: 40, vertexCount: 3, skip: 1),
            };

            // No tier clears 10; the lowest authored tier answers rather than nothing.
            Assert.AreEqual(3, BigScoreTrailBehaviour.SelectTier(tiers, 10).VertexCount);
        }

        [Test]
        public void PrincipalIdFor_DelegatesToResolvedHandler()
        {
            var handler = new SpyBehaviour();
            var resolver = BuildResolver(
                handler, new FakeConfig(new ScoreTrailBehaviourEntry(ScoreTrailBehaviourId.DefaultScore, 0)));
            var msg = new ScorePointsGroupMessage(Red, Vector3.zero, points: 3, lastScore: 7, multiplier: 1);

            var id = resolver.PrincipalIdFor(msg);

            Assert.AreEqual(new TrailId(Red, msg.FirstScore), id);
            Assert.AreEqual(1, handler.PrincipalCalls);
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

        private static BigScoreTierConfig Tier(int minPoints, int vertexCount, int skip)
        {
            return new BigScoreTierConfig(
                minPoints,
                vertexCount,
                skip,
                repeats: 1,
                nestScale: 0.381966f,
                nestRotationDegrees: 0f,
                baseRadius: 2f,
                deployDuration: 0.25f,
                drawDuration: 0.35f,
                collapseDuration: 0.5f,
                ribbonTime: 0.8f,
                rotationSpeedDegrees: 0f,
                driftToTarget: 0.6f);
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
            public IReadOnlyList<BigScoreTierConfig> BigScoreTiers { get; } = System.Array.Empty<BigScoreTierConfig>();

            public FakeConfig(params ScoreTrailBehaviourEntry[] entries)
            {
                _entries = entries;
            }
        }
    }
}
