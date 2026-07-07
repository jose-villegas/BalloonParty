using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    [TestFixture]
    public class ProjectileHitResolverTests
    {
        private IHitDispatcher _hitDispatcher;
        private IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private ColorStreakTracker _streakTracker;
        private ProjectileHitResolver _resolver;
        private ProjectileModel _projectile;

        [SetUp]
        public void SetUp()
        {
            _hitDispatcher = Substitute.For<IHitDispatcher>();
            _shieldGainedPublisher = Substitute.For<IPublisher<ShieldGainedMessage>>();

            var levelUpSubscriber = Substitute.For<ISubscriber<ScoreLevelUpMessage>>();
            levelUpSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ScoreLevelUpMessage>>(),
                    Arg.Any<MessageHandlerFilter<ScoreLevelUpMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            var projectileLoadedSubscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            projectileLoadedSubscriber
                .Subscribe(
                    Arg.Any<IMessageHandler<ProjectileLoadedMessage>>(),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());

            _streakTracker = new ColorStreakTracker(levelUpSubscriber, projectileLoadedSubscriber);

            _resolver = new ProjectileHitResolver(_hitDispatcher, _shieldGainedPublisher, _streakTracker);
            _projectile = new ProjectileModel { IsFree = true };
        }

        [Test]
        public void Resolve_AbsorbingBalloon_ReturnsDestroyed()
        {
            var result = _resolver.Resolve(_projectile, AbsorbingBalloon(), Vector3.zero);

            Assert.AreEqual(ProjectileHitVisual.Destroyed, result);
        }

        [Test]
        public void Resolve_AbsorbingBalloon_SetsProjectileNotFree()
        {
            _resolver.Resolve(_projectile, AbsorbingBalloon(), Vector3.zero);

            Assert.IsFalse(_projectile.IsFree);
        }

        [Test]
        public void Resolve_AbsorbingBalloon_DispatchesActorHitWithAbsorbOutcome()
        {
            var balloon = AbsorbingBalloon();

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            _hitDispatcher.Received(1).Dispatch(Arg.Is<ActorHitMessage>(m =>
                m.Actor == balloon && m.Outcome == HitOutcome.Absorb));
        }

        [Test]
        public void Resolve_PopNormalBalloon_StealsColour()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            _projectile.ColorName.Value = "Blue";

            var result = _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual("Red", _projectile.ColorName.Value);
            Assert.AreEqual(ProjectileHitVisual.Recolored, result);
        }

        [Test]
        public void Resolve_PopRainbowBalloon_DoesNotStealColour()
        {
            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            balloon.IsRainbow.Value = true;
            _projectile.ColorName.Value = "Blue";

            var result = _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual("Blue", _projectile.ColorName.Value);
            Assert.AreEqual(ProjectileHitVisual.None, result);
        }

        [Test]
        public void Resolve_RainbowPop_StreakAtTwo_GrantsShield()
        {
            // Seeds the tracker as if a prior pop already ran through ScoreController's HitPipeline
            // stage — the shield check reads the tracker directly, same as production (see Resolve's
            // "Dispatch runs the streak stage synchronously" comment); _hitDispatcher is a substitute.
            _streakTracker.Record("Blue", false);
            _streakTracker.Record("Blue", false);

            var balloon = new BalloonModel(new BalloonModelConfig(hitsToPop: 1));
            balloon.Color.Value = "Red";
            balloon.IsRainbow.Value = true;
            _projectile.ColorName.Value = "Blue";

            _resolver.Resolve(_projectile, balloon, Vector3.zero);

            Assert.AreEqual(1, _projectile.ShieldsRemaining.Value);
            Assert.AreEqual("Blue", _projectile.ColorName.Value); // unchanged — no steal
        }

        private static IBalloonModel AbsorbingBalloon()
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.EvaluateHit(Arg.Any<DamageContext>()).Returns(HitOutcome.Absorb);
            return balloon;
        }
    }
}
