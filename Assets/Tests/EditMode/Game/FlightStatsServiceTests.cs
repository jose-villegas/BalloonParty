using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Game.Flight;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class FlightStatsServiceTests
    {
        private FlightStatsService _service;
        private ProjectileModel _ball;
        private ProjectileRegistry _registry;
        private IMessageHandler<ProjectileLoadedMessage> _loaded;
        private IMessageHandler<ProjectileFiredMessage> _fired;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyed;

        [SetUp]
        public void SetUp()
        {
            _ball = new ProjectileModel();
            _registry = new ProjectileRegistry();
            _service = new FlightStatsService(
                _registry,
                CaptureSubscriber<ProjectileLoadedMessage>(h => _loaded = h),
                CaptureSubscriber<ProjectileFiredMessage>(h => _fired = h),
                CaptureSubscriber<ProjectileDestroyedMessage>(h => _destroyed = h));
            _service.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Dispose();
        }

        [Test]
        public void PopsOf_CountsPerType_WithoutBleedingBetweenTypes()
        {
            Load();
            Record(BalloonType.Tough, HitOutcome.Pop);
            Record(BalloonType.Tough, HitOutcome.Pop);
            Record(BalloonType.Rainbow, HitOutcome.Pop);

            Assert.AreEqual(2, _service.Of(_ball).PopsOf(BalloonType.Tough));
            Assert.AreEqual(1, _service.Of(_ball).PopsOf(BalloonType.Rainbow));
            Assert.AreEqual(0, _service.Of(_ball).PopsOf(BalloonType.Unbreakable));
        }

        [Test]
        public void DeflectsOf_IsSeparateFromPopsOfTheSameType()
        {
            Load();
            Record(BalloonType.Unbreakable, HitOutcome.Pop);
            Record(BalloonType.Unbreakable, HitOutcome.Deflect);
            Record(BalloonType.Unbreakable, HitOutcome.Deflect);

            Assert.AreEqual(1, _service.Of(_ball).PopsOf(BalloonType.Unbreakable));
            Assert.AreEqual(2, _service.Of(_ball).DeflectsOf(BalloonType.Unbreakable));
        }

        [Test]
        public void Record_NonBalloonActor_IsIgnoredAndDoesNotThrow()
        {
            Load();

            Assert.DoesNotThrow(() => _service.Record(
                new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.Pop)));
            Assert.AreEqual(0, _service.Of(_ball).PopsOf(BalloonType.Simple));
        }

        [Test]
        public void Loading_TheNextProjectile_StartsItsCountersAtZero()
        {
            Load();
            _fired.Handle(new ProjectileFiredMessage(_ball, Vector3.zero, Vector3.up));
            Record(BalloonType.Tough, HitOutcome.Pop);
            Record(BalloonType.Unbreakable, HitOutcome.Deflect);
            _service.RecordWallHit(_ball);
            _service.RecordPierceDischarge(_ball);
            _destroyed.Handle(new ProjectileDestroyedMessage(_ball));
            _registry.Retire(_ball);

            var next = new ProjectileModel();
            LoadBall(next);

            var stats = _service.Of(next);
            Assert.AreEqual(0, stats.PopsOf(BalloonType.Tough));
            Assert.AreEqual(0, stats.DeflectsOf(BalloonType.Unbreakable));
            Assert.AreEqual(0, stats.WallBounces);
            Assert.AreEqual(0, stats.PierceDischarges);
        }

        [Test]
        public void Record_HitWithNoShotBehindIt_CountsInNoFlight()
        {
            Load();

            RecordFor(null, BalloonType.Tough, HitOutcome.Pop);

            Assert.AreEqual(0, _service.Of(_ball).PopsOf(BalloonType.Tough));
            Assert.AreEqual(0, _service.Of(null).PopsOf(BalloonType.Tough));
        }

        [Test]
        public void Of_UnknownBall_ReadsAsAllZeroes()
        {
            Load();
            Record(BalloonType.Tough, HitOutcome.Pop);

            Assert.AreEqual(0, _service.Of(new ProjectileModel()).PopsOf(BalloonType.Tough));
        }

        // The thrower loads the next ball from inside the dead one's Destroyed dispatch, and the other
        // subscribers of that dispatch still read its stats until the registry retires it.
        [Test]
        public void Of_DeadBall_StaysReadableThroughTheNextLoad_UntilRetired()
        {
            Load();
            _registry.Launch(_ball);
            _fired.Handle(new ProjectileFiredMessage(_ball, Vector3.zero, Vector3.up));
            Record(BalloonType.Tough, HitOutcome.Pop);
            _destroyed.Handle(new ProjectileDestroyedMessage(_ball));
            _registry.Land(_ball);

            LoadBall(new ProjectileModel());
            Assert.AreEqual(1, _service.Of(_ball).PopsOf(BalloonType.Tough));

            _registry.Retire(_ball);

            Assert.AreEqual(0, _service.Of(_ball).PopsOf(BalloonType.Tough), "retired balls are forgotten");
        }

        [Test]
        public void Of_RecycledRecord_StartsCleanForALaterBall()
        {
            Load();
            _registry.Launch(_ball);
            _fired.Handle(new ProjectileFiredMessage(_ball, Vector3.zero, Vector3.up));
            Record(BalloonType.Tough, HitOutcome.Pop);
            _service.RecordWallHit(_ball);
            _destroyed.Handle(new ProjectileDestroyedMessage(_ball));
            _registry.Retire(_ball);

            var later = new ProjectileModel();
            LoadBall(later);

            Assert.AreEqual(0, _service.Of(later).PopsOf(BalloonType.Tough));
            Assert.AreEqual(0, _service.Of(later).WallBounces);
        }

        [Test]
        public void Retire_BallThatNeverPublishedDestroyed_ClosesTheWindows()
        {
            Load();
            _registry.Launch(_ball);
            _fired.Handle(new ProjectileFiredMessage(_ball, Vector3.zero, Vector3.up));
            Assert.IsTrue(_service.IsAirborne.Value);

            _registry.Retire(_ball);

            Assert.IsFalse(_service.IsAirborne.Value);
        }

        [Test]
        public void IsAirborne_SpansFiredToDestroyed_NotAiming()
        {
            Assert.IsFalse(_service.IsAirborne.Value, "before loading");

            Load();
            Assert.IsFalse(_service.IsAirborne.Value, "aiming is not airborne");

            _fired.Handle(new ProjectileFiredMessage(_ball, Vector3.zero, Vector3.up));
            Assert.IsTrue(_service.IsAirborne.Value, "fired");

            _destroyed.Handle(new ProjectileDestroyedMessage(_ball));
            Assert.IsFalse(_service.IsAirborne.Value, "destroyed");
        }

        // The bounce counter used to reset on Fired; here it resets on Loaded with everything else.
        // Those are only equivalent because a projectile cannot strike a wall before it launches — if
        // that ever changes, the first bounce of a flight would ramp from a stale count.
        [Test]
        public void WallBounces_AreZeroBetweenLoadingAndFiring()
        {
            Load();

            Assert.AreEqual(0, _service.Of(_ball).WallBounces, "no wall hit can arrive before the shot is fired");

            _fired.Handle(new ProjectileFiredMessage(_ball, Vector3.zero, Vector3.up));
            _service.RecordWallHit(_ball);

            Assert.AreEqual(1, _service.Of(_ball).WallBounces);
        }

        private void Load()
        {
            LoadBall(_ball);
        }

        private void LoadBall(IProjectileModel ball)
        {
            _registry.Load(ball, null);
            _loaded.Handle(new ProjectileLoadedMessage(ball));
        }

        private void Record(BalloonType type, HitOutcome outcome)
        {
            RecordFor(_ball, type, outcome);
        }

        private void RecordFor(IProjectileModel ball, BalloonType type, HitOutcome outcome)
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.TypeName.Returns(type);
            _service.Record(new ActorHitMessage(
                balloon, Vector3.zero, Vector3.zero, outcome, new DamageContext(1, DamageFlags.Normal, "", ball)));
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(
                    Arg.Do(capture),
                    Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
