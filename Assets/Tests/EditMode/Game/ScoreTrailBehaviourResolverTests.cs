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

            public FakeConfig(params ScoreTrailBehaviourEntry[] entries)
            {
                _entries = entries;
            }
        }
    }
}
