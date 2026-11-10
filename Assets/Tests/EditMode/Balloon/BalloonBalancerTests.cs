using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Projectile;
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
        private ProjectileRegistry _registry;
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

            _registry = new ProjectileRegistry();

            // DisturbanceField is only dereferenced when animating a non-empty balance, so these
            // grid-only tests can omit it.
            _balancer = new BalloonBalancer(
                _grid, balanceQuery, _balloonsConfig, pathHolder,
                CaptureBalanceRequests(),
                _registry, _pauseService, null, new BalloonMotionTicker());
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
        public void HasPossibleMove_UnanchoredUnbreakable_True()
        {
            PlaceUnbreakable(new Vector2Int(1, 1), anchored: false);

            Assert.IsTrue(_balancer.HasPossibleMove());
        }

        [Test]
        public void HasPossibleMove_AnchoredUnbreakableOverAGap_False()
        {
            PlaceUnbreakable(new Vector2Int(1, 1), anchored: true);

            Assert.IsFalse(_balancer.HasPossibleMove(), "a formation member holds its slot, so it is no move");
            Assert.IsFalse(_balancer.TickFlightRebalance(5f), "and the flight pulse does not fire for it");
        }

        [Test]
        public void HasPossibleMove_AnchoredUnbreakableAndAFreeBalloon_True()
        {
            PlaceUnbreakable(new Vector2Int(0, 1), anchored: true);
            _grid.Place(new BalloonModel(), null, new Vector2Int(2, 1));

            Assert.IsTrue(_balancer.HasPossibleMove(), "the anchor does not hide the roamer next to it");
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
        public void FlightPulse_NoBallInFlight_StillTriggers()
        {
            // Lines arrive on the spawn clock, not after a shot, so the board keeps settling while
            // nothing flies.
            PlaceUnbalancedBalloon();

            Assert.IsTrue(_balancer.TickFlightRebalance(5f));
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

        [Test]
        public void FlightPulse_OnlyBallInFlightIsOnItsLastShieldApproach_FreezesTheBoard()
        {
            PlaceUnbalancedBalloon();
            LoadFreeProjectile().IsLastShieldApproach.Value = true;

            Assert.IsFalse(_balancer.TickFlightRebalance(5f));
        }

        [Test]
        public void FlightPulse_OneOfTwoBallsDoomed_KeepsRebalancing()
        {
            PlaceUnbalancedBalloon();
            LoadFreeProjectile().IsLastShieldApproach.Value = true;
            LoadFreeProjectile();

            Assert.IsTrue(_balancer.TickFlightRebalance(5f), "one ball's doom never freezes the board under the other");
        }

        // A balloon at (1,1) over an empty (1,0) — unbalanced and free to fall.
        private void PlaceUnbalancedBalloon()
        {
            _grid.Place(new BalloonModel(), null, new Vector2Int(1, 1));
        }

        private void PlaceUnbreakable(Vector2Int slot, bool anchored)
        {
            var model = new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable));
            model.SetInFormation(anchored);
            _grid.Place(model, null, slot);
        }

        private void RequestBalance()
        {
            _balanceHandler.Handle(default);
        }

        private ProjectileModel LoadFreeProjectile()
        {
            var projectile = new ProjectileModel { IsFree = true };
            _registry.Load(projectile, null);
            _registry.Launch(projectile);
            return projectile;
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
    }
}
