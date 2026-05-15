using System.Reflection;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Nudge
{
    [TestFixture]
    public class NudgeServiceTests
    {
        private NudgeService _service;
        private BalloonsConfiguration _balloonsConfig;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(6, 10));
            gameConfig.SlotSeparation.Returns(new Vector2(1f, 0.85f));
            gameConfig.SlotsOffset.Returns(new Vector2(2.5f, 4f));

            _balloonsConfig = ScriptableObject.CreateInstance<BalloonsConfiguration>();
            SetField(_balloonsConfig, "_nudgeDistance", 0.3f);
            SetField(_balloonsConfig, "_nudgeDuration", 0.15f);
            SetField(_balloonsConfig, "_nudgeFalloff", 1.5f);

            var grid = new BalloonParty.Slots.SlotGrid(gameConfig);
            var hitSubscriber = Substitute.For<ISubscriber<BalloonHitMessage>>();
            var nudgeSubscriber = Substitute.For<ISubscriber<BalloonNudgeMessage>>();

            _service = new NudgeService(grid, _balloonsConfig, hitSubscriber, nudgeSubscriber);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_balloonsConfig);
        }


        [Test]
        public void ResolveDistance_BalloonOverridePresent_UsesOverride()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Deflect, 1.5f) };

            var result = _service.ResolveDistance(overrides, null, NudgeType.Deflect);

            Assert.AreEqual(1.5f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_OnlyPublisherOverride_UsesPublisherOverride()
        {
            var pubOverrides = new[] { new NudgeOverride(NudgeType.Deflect, 2.0f) };

            var result = _service.ResolveDistance(null, pubOverrides, NudgeType.Deflect);

            Assert.AreEqual(2.0f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_NoOverrides_UsesConfigDefault()
        {
            var result = _service.ResolveDistance(null, null, NudgeType.Deflect);

            Assert.AreEqual(0.3f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_BalloonOverrideTakesPriorityOverPublisher()
        {
            var balloonOverrides = new[] { new NudgeOverride(NudgeType.Deflect, 1.0f) };
            var pubOverrides = new[] { new NudgeOverride(NudgeType.Deflect, 2.0f) };

            var result = _service.ResolveDistance(balloonOverrides, pubOverrides, NudgeType.Deflect);

            Assert.AreEqual(1.0f, result, 0.001f);
        }


        [Test]
        public void ResolveDuration_BalloonOverridePresent_UsesOverride()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Neighbor, 0f, 0.5f) };

            var result = _service.ResolveDuration(overrides, null, NudgeType.Neighbor);

            Assert.AreEqual(0.5f, result, 0.001f);
        }

        [Test]
        public void ResolveDuration_NoOverrides_UsesConfigDefault()
        {
            var result = _service.ResolveDuration(null, null, NudgeType.Neighbor);

            Assert.AreEqual(0.15f, result, 0.001f);
        }


        [Test]
        public void ResolveFalloff_OverridePresent_UsesOverride()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Shockwave, 0f, 0f, 3.0f) };

            var result = _service.ResolveFalloff(overrides, NudgeType.Shockwave);

            Assert.AreEqual(3.0f, result, 0.001f);
        }

        [Test]
        public void ResolveFalloff_NoOverride_UsesConfigDefault()
        {
            var result = _service.ResolveFalloff(null, NudgeType.Shockwave);

            Assert.AreEqual(1.5f, result, 0.001f);
        }


        [Test]
        public void ResolveDistance_AllFlagOverride_MatchesAnySource()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.All, 4.0f) };

            Assert.AreEqual(4.0f, _service.ResolveDistance(overrides, null, NudgeType.Deflect), 0.001f);
            Assert.AreEqual(4.0f, _service.ResolveDistance(overrides, null, NudgeType.Neighbor), 0.001f);
            Assert.AreEqual(4.0f, _service.ResolveDistance(overrides, null, NudgeType.Shockwave), 0.001f);
        }

        [Test]
        public void ResolveDistance_MismatchedFlag_FallsThrough()
        {
            // Override only applies to Shockwave, but source is Deflect
            var overrides = new[] { new NudgeOverride(NudgeType.Shockwave, 5.0f) };

            var result = _service.ResolveDistance(overrides, null, NudgeType.Deflect);

            Assert.AreEqual(0.3f, result, 0.001f); // falls through to config default
        }

        private static void SetField(object target, string fieldName, object value)
        {
            target.GetType()
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)!
                .SetValue(target, value);
        }
    }
}
