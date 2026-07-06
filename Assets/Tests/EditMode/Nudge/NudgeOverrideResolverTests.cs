using BalloonParty.Configuration;
using BalloonParty.Nudge;
using NSubstitute;
using NUnit.Framework;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Tests.Nudge
{
    [TestFixture]
    public class NudgeOverrideResolverTests
    {
        private NudgeOverrideResolver _resolver;

        [SetUp]
        public void SetUp()
        {
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.NudgeDistance.Returns(0.3f);
            balloonsConfig.NudgeDuration.Returns(0.15f);
            balloonsConfig.NudgeFalloff.Returns(1.5f);

            _resolver = new NudgeOverrideResolver(balloonsConfig);
        }

        [Test]
        public void ResolveDistance_BalloonOverridePresent_UsesOverride()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Deflect, 1.5f) };

            var result = _resolver.ResolveDistance(overrides, null, NudgeType.Deflect);

            Assert.AreEqual(1.5f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_OnlyPublisherOverride_UsesPublisherOverride()
        {
            var pubOverrides = new[] { new NudgeOverride(NudgeType.Deflect, 2.0f) };

            var result = _resolver.ResolveDistance(null, pubOverrides, NudgeType.Deflect);

            Assert.AreEqual(2.0f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_NoOverrides_UsesConfigDefault()
        {
            var result = _resolver.ResolveDistance(null, null, NudgeType.Deflect);

            Assert.AreEqual(0.3f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_BalloonOverrideTakesPriorityOverPublisher()
        {
            var balloonOverrides = new[] { new NudgeOverride(NudgeType.Deflect, 1.0f) };
            var pubOverrides = new[] { new NudgeOverride(NudgeType.Deflect, 2.0f) };

            var result = _resolver.ResolveDistance(balloonOverrides, pubOverrides, NudgeType.Deflect);

            Assert.AreEqual(1.0f, result, 0.001f);
        }

        [Test]
        public void ResolveDuration_BalloonOverridePresent_UsesOverride()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Neighbor, 0f, 0.5f) };

            var result = _resolver.ResolveDuration(overrides, null, NudgeType.Neighbor);

            Assert.AreEqual(0.5f, result, 0.001f);
        }

        [Test]
        public void ResolveDuration_NoOverrides_UsesConfigDefault()
        {
            var result = _resolver.ResolveDuration(null, null, NudgeType.Neighbor);

            Assert.AreEqual(0.15f, result, 0.001f);
        }

        [Test]
        public void ResolveFalloff_OverridePresent_UsesOverride()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Shockwave, 0f, 0f, 3.0f) };

            var result = _resolver.ResolveFalloff(overrides, NudgeType.Shockwave);

            Assert.AreEqual(3.0f, result, 0.001f);
        }

        [Test]
        public void ResolveFalloff_NoOverride_UsesConfigDefault()
        {
            var result = _resolver.ResolveFalloff(null, NudgeType.Shockwave);

            Assert.AreEqual(1.5f, result, 0.001f);
        }

        [Test]
        public void ResolveDistance_AllFlagOverride_MatchesAnySource()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.All, 4.0f) };

            Assert.AreEqual(4.0f, _resolver.ResolveDistance(overrides, null, NudgeType.Deflect), 0.001f);
            Assert.AreEqual(4.0f, _resolver.ResolveDistance(overrides, null, NudgeType.Neighbor), 0.001f);
            Assert.AreEqual(4.0f, _resolver.ResolveDistance(overrides, null, NudgeType.Shockwave), 0.001f);
        }

        [Test]
        public void ResolveDistance_MismatchedFlag_FallsThrough()
        {
            var overrides = new[] { new NudgeOverride(NudgeType.Shockwave, 5.0f) };

            var result = _resolver.ResolveDistance(overrides, null, NudgeType.Deflect);

            Assert.AreEqual(0.3f, result, 0.001f);
        }
    }
}
