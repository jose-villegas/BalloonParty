using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Game.Flight;
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
        private IMessageHandler<ProjectileLoadedMessage> _loaded;
        private IMessageHandler<ProjectileFiredMessage> _fired;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyed;

        [SetUp]
        public void SetUp()
        {
            _service = new FlightStatsService(
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

            Assert.AreEqual(2, _service.PopsOf(BalloonType.Tough));
            Assert.AreEqual(1, _service.PopsOf(BalloonType.Rainbow));
            Assert.AreEqual(0, _service.PopsOf(BalloonType.Unbreakable));
        }

        [Test]
        public void DeflectsOf_IsSeparateFromPopsOfTheSameType()
        {
            Load();
            Record(BalloonType.Unbreakable, HitOutcome.Pop);
            Record(BalloonType.Unbreakable, HitOutcome.Deflect);
            Record(BalloonType.Unbreakable, HitOutcome.Deflect);

            Assert.AreEqual(1, _service.PopsOf(BalloonType.Unbreakable));
            Assert.AreEqual(2, _service.DeflectsOf(BalloonType.Unbreakable));
        }

        [Test]
        public void Record_NonBalloonActor_IsIgnoredAndDoesNotThrow()
        {
            Load();

            Assert.DoesNotThrow(() => _service.Record(
                new ActorHitMessage(null, Vector3.zero, Vector3.zero, HitOutcome.Pop)));
            Assert.AreEqual(0, _service.PopsOf(BalloonType.Simple));
        }

        [Test]
        public void Loading_TheNextProjectile_ZeroesEveryCounter()
        {
            Load();
            _fired.Handle(default);
            Record(BalloonType.Tough, HitOutcome.Pop);
            Record(BalloonType.Unbreakable, HitOutcome.Deflect);
            _service.RecordWallHit();
            _service.RecordPierceDischarge();

            Load();

            Assert.AreEqual(0, _service.PopsOf(BalloonType.Tough));
            Assert.AreEqual(0, _service.DeflectsOf(BalloonType.Unbreakable));
            Assert.AreEqual(0, _service.WallBounces);
            Assert.AreEqual(0, _service.PierceDischarges);
        }

        [Test]
        public void FlightIndex_IncrementsOncePerLoadedProjectile()
        {
            Load();
            var first = _service.FlightIndex;
            Load();

            Assert.AreEqual(first + 1, _service.FlightIndex);
        }

        [Test]
        public void IsLoaded_SpansLoadedToDestroyed_AndIsAirborneOnlySpansFiredToDestroyed()
        {
            Assert.IsFalse(_service.IsLoaded.Value, "before loading");
            Assert.IsFalse(_service.IsAirborne.Value, "before loading");

            Load();
            Assert.IsTrue(_service.IsLoaded.Value, "loaded but not fired");
            Assert.IsFalse(_service.IsAirborne.Value, "aiming is not airborne");

            _fired.Handle(default);
            Assert.IsTrue(_service.IsLoaded.Value, "fired");
            Assert.IsTrue(_service.IsAirborne.Value, "fired");

            _destroyed.Handle(default);
            Assert.IsFalse(_service.IsLoaded.Value, "destroyed");
            Assert.IsFalse(_service.IsAirborne.Value, "destroyed");
        }

        // The bounce counter used to reset on Fired; here it resets on Loaded with everything else.
        // Those are only equivalent because a projectile cannot strike a wall before it launches — if
        // that ever changes, the first bounce of a flight would ramp from a stale count.
        [Test]
        public void WallBounces_AreZeroBetweenLoadingAndFiring()
        {
            Load();

            Assert.AreEqual(0, _service.WallBounces, "no wall hit can arrive before the shot is fired");

            _fired.Handle(default);
            _service.RecordWallHit();

            Assert.AreEqual(1, _service.WallBounces);
        }

        private void Load()
        {
            _loaded.Handle(default);
        }

        private void Record(BalloonType type, HitOutcome outcome)
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.TypeName.Returns(type);
            _service.Record(new ActorHitMessage(balloon, Vector3.zero, Vector3.zero, outcome));
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
