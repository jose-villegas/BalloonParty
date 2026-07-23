using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Grid;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class BalloonBalancerTests
    {
        private SlotGrid _grid;
        private IBalloonsConfiguration _balloonsConfig;
        private PauseService _pauseService;
        private IMessageHandler<ProjectileLoadedMessage> _loadedHandler;
        private IMessageHandler<BalanceBalloonsMessage> _balanceHandler;
        private BalloonBalancer _balancer;

        [SetUp]
        public void SetUp()
        {
            var gameConfig = Substitute.For<ISlotGridConfig>();
            gameConfig.SlotsSize.Returns(new Vector2Int(3, 3));
            var pathHolder = new BalancePathHolder();
            _grid = new SlotGrid(gameConfig, pathHolder);
            var balanceQuery = new GridBalanceQuery(_grid);

            _balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            _balloonsConfig.FlightRebalanceInterval.Returns(1f);

            _pauseService = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());

            // DisturbanceField is only dereferenced when animating a non-empty balance, so these
            // grid-only tests can omit it.
            _balancer = new BalloonBalancer(
                _grid, balanceQuery, _balloonsConfig, pathHolder,
                CaptureBalanceRequests(),
                CaptureLoaded(), StubSubscriber<ProjectileDestroyedMessage>(),
                _pauseService, null, new BalloonMotionTicker());
            _balancer.Start();
        }

        [TearDown]
        public void TearDown()
        {
            // A flight pulse crossing the interval schedules a fire-and-forget balance; bump the generation
            // so that orphaned continuation drops instead of running against this viewless grid post-test.
            _balancer.ResetRun(_balancer.Generation + 1);
        }

        [Test]
        public void RunScheduledBalance_WithPendingRequest_Runs()
        {
            RequestBalance();

            Assert.IsTrue(_balancer.RunScheduledBalance(_balancer.Generation));
        }

        [Test]
        public void RunScheduledBalance_WithoutPendingRequest_DoesNotRun()
        {
            Assert.IsFalse(_balancer.RunScheduledBalance(_balancer.Generation));
        }

        [Test]
        public void RunScheduledBalance_AfterDirectBalance_DoesNotRun()
        {
            RequestBalance();

            // The direct run (e.g. the spawner's pre-spawn sweep) services the pending request.
            _balancer.Balance();

            Assert.IsFalse(_balancer.RunScheduledBalance(_balancer.Generation));
        }

        [Test]
        public void RunScheduledBalance_AfterReset_WithStaleGeneration_DoesNotRun()
        {
            var staleGeneration = _balancer.Generation;

            _balancer.ResetRun(staleGeneration + 1);

            Assert.IsFalse(_balancer.RunScheduledBalance(staleGeneration));
        }

        [Test]
        public void ResetRun_AdoptsTheGivenGeneration()
        {
            _balancer.ResetRun(7);

            Assert.AreEqual(7, _balancer.Generation);
        }

        [Test]
        public void HasPossibleMove_UnbalancedBalloon_True()
        {
            PlaceUnbalancedBalloon();

            Assert.IsTrue(_balancer.HasPossibleMove());
        }

        [Test]
        public void HasPossibleMove_EmptyBoard_False()
        {
            Assert.IsFalse(_balancer.HasPossibleMove());
        }

        [Test]
        public void FlightPulse_AirborneWithMoveAvailable_TriggersAtInterval()
        {
            PlaceUnbalancedBalloon();
            LoadFreeProjectile();

            Assert.IsFalse(_balancer.TickFlightRebalance(0.5f), "below the 1s interval");
            Assert.IsTrue(_balancer.TickFlightRebalance(0.6f), "crossed the interval with a move available");
        }

        [Test]
        public void FlightPulse_NoProjectile_DoesNotTrigger()
        {
            PlaceUnbalancedBalloon();

            Assert.IsFalse(_balancer.TickFlightRebalance(5f));
        }

        [Test]
        public void FlightPulse_BoardSettled_DoesNotTrigger()
        {
            LoadFreeProjectile();

            Assert.IsFalse(_balancer.TickFlightRebalance(5f));
        }

        [Test]
        public void FlightPulse_WhilePaused_DoesNotTrigger()
        {
            PlaceUnbalancedBalloon();
            LoadFreeProjectile();
            _pauseService.Pause(PauseSource.LevelTransition);

            Assert.IsFalse(_balancer.TickFlightRebalance(5f));
        }

        [Test]
        public void FlightPulse_IntervalZero_Disabled()
        {
            _balloonsConfig.FlightRebalanceInterval.Returns(0f);
            PlaceUnbalancedBalloon();
            LoadFreeProjectile();

            Assert.IsFalse(_balancer.TickFlightRebalance(5f));
        }

        // A balloon at (1,1) over an empty (1,0) — unbalanced and free to fall.
        private void PlaceUnbalancedBalloon()
        {
            _grid.Place(new BalloonModel(), null, new Vector2Int(1, 1));
        }

        private void RequestBalance()
        {
            _balanceHandler.Handle(default);
        }

        private void LoadFreeProjectile()
        {
            _loadedHandler.Handle(new ProjectileLoadedMessage(new ProjectileModel { IsFree = true }));
        }

        private ISubscriber<BalanceBalloonsMessage> CaptureBalanceRequests()
        {
            var subscriber = Substitute.For<ISubscriber<BalanceBalloonsMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<BalanceBalloonsMessage>>(h => _balanceHandler = h),
                    Arg.Any<MessageHandlerFilter<BalanceBalloonsMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }

        private ISubscriber<ProjectileLoadedMessage> CaptureLoaded()
        {
            var subscriber = Substitute.For<ISubscriber<ProjectileLoadedMessage>>();
            subscriber
                .Subscribe(
                    Arg.Do<IMessageHandler<ProjectileLoadedMessage>>(h => _loadedHandler = h),
                    Arg.Any<MessageHandlerFilter<ProjectileLoadedMessage>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }

        private static ISubscriber<T> StubSubscriber<T>()
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(Arg.Any<IMessageHandler<T>>(), Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
