using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Game.Flight;
using BalloonParty.Game.Score;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;
using UnityEngine;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     Two balls in flight at once through the per-ball stores: the registry that says which exist, the
    ///     flight stats that count what each did, and the colour streaks that each climb on their own.
    ///     Mirrors the view's exit order: land, publish Destroyed, retire.
    /// </summary>
    [TestFixture]
    public class ConcurrentBallsTests
    {
        private ProjectileRegistry _registry;
        private FlightStatsService _stats;
        private ColorStreakTracker _streaks;
        private IMessageHandler<ProjectileLoadedMessage> _loaded;
        private IMessageHandler<ProjectileLoadedMessage> _streakLoaded;
        private IMessageHandler<ProjectileFiredMessage> _fired;
        private IMessageHandler<ProjectileDestroyedMessage> _destroyed;
        private ProjectileModel _ballA;
        private ProjectileModel _ballB;

        [SetUp]
        public void SetUp()
        {
            _registry = new ProjectileRegistry();

            _stats = new FlightStatsService(
                _registry,
                Capture<ProjectileLoadedMessage>(h => _loaded = h),
                Capture<ProjectileFiredMessage>(h => _fired = h),
                Capture<ProjectileDestroyedMessage>(h => _destroyed = h));
            _stats.Start();

            _streaks = new ColorStreakTracker(
                Substitute.For<IPublisher<StreakChangedMessage>>(),
                Capture<ScoreLevelUpMessage>(_ => { }),
                Capture<ProjectileLoadedMessage>(h => _streakLoaded = h),
                _registry);

            _ballA = new ProjectileModel();
            _ballB = new ProjectileModel();
        }

        [TearDown]
        public void TearDown()
        {
            _stats.Dispose();
            _streaks.Dispose();
        }

        [Test]
        public void Registry_TwoBallsFired_BothAreInFlight_OldestFirst()
        {
            Fly(_ballA);
            Fly(_ballB);

            Assert.AreEqual(2, _registry.InFlightCount);
            Assert.AreSame(_ballA, _registry.InFlightAt(0));
            Assert.AreSame(_ballB, _registry.InFlightAt(1));
            Assert.IsTrue(_registry.IsInFlight(_ballA));
            Assert.IsTrue(_registry.IsInFlight(_ballB));
        }

        [Test]
        public void Registry_LandingOne_LeavesTheOtherFlying()
        {
            Fly(_ballA);
            Fly(_ballB);

            _registry.Land(_ballA);

            Assert.AreEqual(1, _registry.InFlightCount);
            Assert.IsFalse(_registry.IsAlive(_ballA));
            Assert.IsTrue(_registry.IsAlive(_ballB));
            Assert.AreSame(_ballB, _registry.InFlightAt(0));
        }

        [Test]
        public void Registry_ShotOrCurrent_NeverSwapsADeadBallForALiveOne()
        {
            Fly(_ballA);
            Fly(_ballB);
            _registry.Land(_ballA);

            Assert.IsNull(_registry.ShotOrCurrent(_ballA), "an effect of a dead ball lands on nobody");
            Assert.AreSame(_ballB, _registry.ShotOrCurrent(_ballB));
        }

        [Test]
        public void Registry_ShotOrCurrent_WithNoShot_FollowsTheLatestInFlightThenTheLoaded()
        {
            Fly(_ballA);
            Fly(_ballB);
            var loaded = new ProjectileModel();
            _registry.Load(loaded, null);

            Assert.AreSame(_ballB, _registry.ShotOrCurrent(null), "latest fired beats the loaded one");

            _registry.Land(_ballA);
            _registry.Land(_ballB);

            Assert.AreSame(loaded, _registry.ShotOrCurrent(null));
        }

        [Test]
        public void Registry_Retire_FiresOncePerBall_AndIsIdempotent()
        {
            Fly(_ballA);
            Fly(_ballB);
            var retired = 0;
            IProjectileModel last = null;
            _registry.Retired.Subscribe(p =>
            {
                retired++;
                last = p;
            });

            _registry.Retire(_ballA);
            _registry.Retire(_ballA);

            Assert.AreEqual(1, retired);
            Assert.AreSame(_ballA, last);
            Assert.IsTrue(_registry.IsAlive(_ballB), "retiring one leaves the other");
        }

        [Test]
        public void FlightStats_OneBallsPopsAndWallBounces_DoNotShowInTheOthers()
        {
            Fly(_ballA);
            Fly(_ballB);

            Pop(_ballA, BalloonType.Tough);
            Pop(_ballA, BalloonType.Tough);
            _stats.RecordWallHit(_ballA);
            _stats.RecordPierceDischarge(_ballA);
            Pop(_ballB, BalloonType.Rainbow);

            Assert.AreEqual(2, _stats.Of(_ballA).PopsOf(BalloonType.Tough));
            Assert.AreEqual(1, _stats.Of(_ballA).WallBounces);
            Assert.AreEqual(1, _stats.Of(_ballA).PierceDischarges);
            Assert.AreEqual(0, _stats.Of(_ballB).PopsOf(BalloonType.Tough));
            Assert.AreEqual(0, _stats.Of(_ballB).WallBounces);
            Assert.AreEqual(0, _stats.Of(_ballB).PierceDischarges);
            Assert.AreEqual(1, _stats.Of(_ballB).PopsOf(BalloonType.Rainbow));
        }

        [Test]
        public void FlightStats_DyingBallsStats_StayReadableThroughItsDestroyedDispatch_ThenGoOnRetire()
        {
            Fly(_ballA);
            Fly(_ballB);
            Pop(_ballA, BalloonType.Tough);
            Pop(_ballB, BalloonType.Tough);
            Pop(_ballB, BalloonType.Tough);

            _registry.Land(_ballA);
            _destroyed.Handle(new ProjectileDestroyedMessage(_ballA));
            Assert.AreEqual(1, _stats.Of(_ballA).PopsOf(BalloonType.Tough), "readable inside its own dispatch");

            _registry.Retire(_ballA);

            Assert.AreEqual(0, _stats.Of(_ballA).PopsOf(BalloonType.Tough));
            Assert.AreEqual(2, _stats.Of(_ballB).PopsOf(BalloonType.Tough), "the other ball is untouched");
            Assert.IsTrue(_stats.IsAirborne.Value, "B still flies");
        }

        [Test]
        public void FlightStats_OneBallLanding_DoesNotCloseTheAirborneWindowWhileTheOtherFlies()
        {
            Fly(_ballA);
            Fly(_ballB);

            _destroyed.Handle(new ProjectileDestroyedMessage(_ballA));
            Assert.IsTrue(_stats.IsAirborne.Value);

            _destroyed.Handle(new ProjectileDestroyedMessage(_ballB));
            Assert.IsFalse(_stats.IsAirborne.Value);
        }

        [Test]
        public void FlightStats_OrphanedBallClearedWithoutDestroyed_ClosesTheWindows()
        {
            Fly(_ballA);
            Assert.IsTrue(_stats.IsAirborne.Value);

            _registry.Clear();

            Assert.IsFalse(_stats.IsAirborne.Value);
            Assert.IsFalse(_stats.IsLoaded.Value);
        }

        [Test]
        public void Streak_OneBallsPops_DoNotChangeTheOthersStreak()
        {
            Fly(_ballA);
            Fly(_ballB);

            _streaks.For(_ballA).Record("Red");
            _streaks.For(_ballA).Record("Red");
            _streaks.For(_ballB).Record("Blue");

            Assert.AreEqual(2, _streaks.For(_ballA).CurrentStreak);
            Assert.AreEqual("Red", _streaks.For(_ballA).LastColor);
            Assert.AreEqual(1, _streaks.For(_ballB).CurrentStreak);
            Assert.AreEqual("Blue", _streaks.For(_ballB).LastColor);
            Assert.AreEqual(2, _streaks.GetStreak("Red"), "the highest live streak on a colour, across balls");
        }

        [Test]
        public void Streak_LandedBall_StopsCounting_AndIsDroppedOnRetire_WhileTheOtherSurvives()
        {
            Fly(_ballA);
            Fly(_ballB);
            _streaks.For(_ballA).Record("Red");
            _streaks.For(_ballA).Record("Red");
            _streaks.For(_ballB).Record("Red");

            _registry.Land(_ballA);
            Assert.AreEqual(1, _streaks.GetStreak("Red"), "a landed ball no longer counts, even before it retires");
            Assert.AreEqual(2, _streaks.For(_ballA).CurrentStreak, "but its own streak is readable through its dispatch");

            _registry.Retire(_ballA);

            Assert.AreEqual(0, _streaks.For(_ballA).CurrentStreak, "a retired ball's streak is dropped");
            Assert.AreEqual(1, _streaks.For(_ballB).CurrentStreak);
            Assert.AreEqual("Red", _streaks.For(_ballB).LastColor);
        }

        [Test]
        public void Streak_NextLoad_ResetsOnlyTheSharedStreak_NotALiveBallsOwn()
        {
            Fly(_ballA);
            Fly(_ballB);
            _streaks.For(_ballA).Record("Red");
            _streaks.For(null).Record("Blue");

            _streakLoaded.Handle(new ProjectileLoadedMessage(new ProjectileModel()));

            Assert.AreEqual(1, _streaks.For(_ballA).CurrentStreak, "in-flight balls keep their streaks across loads");
            Assert.AreEqual(0, _streaks.For(null).CurrentStreak);
        }

        private void Fly(ProjectileModel ball)
        {
            _registry.Load(ball, null);
            _loaded.Handle(new ProjectileLoadedMessage(ball));
            _registry.Launch(ball);
            _fired.Handle(new ProjectileFiredMessage(ball, Vector3.zero, Vector3.up));
        }

        private void Pop(IProjectileModel ball, BalloonType type)
        {
            var balloon = Substitute.For<IBalloonModel>();
            balloon.TypeName.Returns(type);
            _stats.Record(new ActorHitMessage(
                balloon, Vector3.zero, Vector3.zero, HitOutcome.Pop,
                new DamageContext(1, DamageFlags.Normal, "", ball)));
        }

        private static ISubscriber<T> Capture<T>(Action<IMessageHandler<T>> capture)
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
