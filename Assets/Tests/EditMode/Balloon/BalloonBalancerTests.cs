using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Game.Assist;
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

        [Test]
        public void ToughSnakeStep_ADoomedShotHoldsItsClock_RatherThanRestartingIt()
        {
            // Nearly every shot ends in a doomed glide; restarting the clock there kept the snakes from ever stepping.
            var balancer = BalancerWithAToughSnake(out var formations);
            var shot = LoadFreeProjectile();

            Assert.IsFalse(balancer.TickFlightRebalance(0.6f));
            shot.IsLastShieldApproach.Value = true;
            Assert.IsFalse(balancer.TickFlightRebalance(5f), "a doomed glide freezes the board, the snake too");
            shot.IsLastShieldApproach.Value = false;

            Assert.IsTrue(balancer.TickFlightRebalance(0.5f), "the step comes 1s of play after the last one");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ToughSnakeStep_FallsDue_WhenNothingElseWouldMove()
        {
            var balancer = BalancerWithAToughSnake(out var formations);

            Assert.IsFalse(balancer.HasPossibleMove());
            Assert.IsTrue(balancer.TickFlightRebalance(1f));
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ToughSnakeStep_KeepsItsOwnInterval_NotTheBoardsPulse()
        {
            var balancer = BalancerWithAToughSnake(out var formations, toughStepSeconds: 0.25f);

            Assert.IsFalse(balancer.TickFlightRebalance(0.2f));
            Assert.IsTrue(balancer.TickFlightRebalance(0.1f), "a step at 0.25s, well before the 1s pulse");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void TheToughs_FormUpWhileTheLevelTransitionHoldsTheBoard()
        {
            var balancer = BalancerWithAToughSnake(out var formations, formAtOnce: false);
            _pauseService.Pause(PauseSource.LevelTransition);

            Assert.IsFalse(balancer.TickFlightRebalance(0.1f), "no balance, no step");
            Assert.IsTrue(formations.HasToughSnake, "but the toughs rising for the next level form up");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void TheToughs_DontFormUpInAnyOtherPause()
        {
            var balancer = BalancerWithAToughSnake(out var formations, formAtOnce: false);
            _pauseService.Pause(PauseSource.LevelTransition);
            _pauseService.Pause(PauseSource.Cheat);

            balancer.TickFlightRebalance(0.1f);

            Assert.IsFalse(formations.HasToughSnake, "a pause holds everything");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void StepClock_ANewSnakeStepsAtOnce_ThenEachInterval()
        {
            var elapsed = 0f;

            Assert.IsFalse(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, false, true, 0.1f), "no snake yet");
            Assert.IsTrue(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, true, 0.01f), "it moves as it forms");
            Assert.IsFalse(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, true, 1f));
            Assert.IsTrue(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, true, 0.8f), "1.8s on");
            Assert.IsFalse(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, true, 1f));
        }

        [Test]
        public void StepClock_AStepDueWhileAMemberRises_ComesTheMomentItLands()
        {
            var elapsed = 0f;
            BalloonBalancer.TickStepClock(ref elapsed, 1.7f, false, true, 0.1f);

            Assert.IsFalse(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, false, 0.5f), "formed while rising");
            Assert.IsFalse(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, false, 0.5f));
            Assert.IsTrue(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, true, 0.01f), "landed: it steps");
            Assert.IsFalse(BalloonBalancer.TickStepClock(ref elapsed, 1.7f, true, true, 1f), "then a full interval");
        }

        [Test]
        public void ToughSnakeStep_TheFirstComesTheMomentTheSnakeForms()
        {
            var balancer = BalancerWithAToughSnake(out var formations, formAtOnce: false);

            Assert.IsTrue(balancer.TickFlightRebalance(0.1f), "the landed toughs are due to form up");
            Assert.IsFalse(formations.HasToughSnake, "which the balance does");
            formations.GatherToughSnake(_ => { });
            Assert.IsTrue(formations.HasToughSnake);
            Assert.IsFalse(formations.ToughsDueToGather(), "all in");

            // Neither the board's pulse nor a gather: only the snake's own clock can make this true.
            Assert.IsTrue(balancer.TickFlightRebalance(0.01f), "its first step, not a full interval later");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ASpawnedTough_FallsInInTheBalanceItsLineCalls_WithoutWaitingForATick()
        {
            var balancer = BalancerWithAToughSnake(out var formations, formAtOnce: false);

            balancer.Balance();

            Assert.IsTrue(formations.HasToughSnake, "it flies from its entry straight to its place in the body");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void TheUnbreakablesCycle_ComesEveryBeatsPerCycleBeats_WhileAnUnbreakableIsOnTheBoard()
        {
            var balancer = BalancerWithAToughSnake(out var formations, toughStepSeconds: 100f, formAtOnce: false);
            _balloonsConfig.UnbreakableFormation.ToughSnakes.Returns(false);
            _balloonsConfig.UnbreakableFormation.BeatSeconds.Returns(1f);
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), null,
                new Vector2Int(1, 1));
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);

            Assert.IsFalse(balancer.TickFlightRebalance(1f), "the first beat: no snake to step, no cycle");
            Assert.IsTrue(balancer.TickFlightRebalance(1f), "the second beat is a cycle");
            Assert.IsFalse(balancer.TickFlightRebalance(1f));
            _grid.Remove(new Vector2Int(1, 1));
            Assert.IsFalse(balancer.TickFlightRebalance(1f), "no unbreakable, nothing to arrange");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void FreeUnbreakables_FormUpOnABeat_BetweenCycles()
        {
            var balancer = BalancerWithAToughSnake(out var formations, toughStepSeconds: 100f, formAtOnce: false);
            _balloonsConfig.UnbreakableFormation.ToughSnakes.Returns(false);
            _balloonsConfig.UnbreakableFormation.BeatSeconds.Returns(1f);
            _balloonsConfig.UnbreakableFormation.MinMembers.Returns(2);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), null,
                new Vector2Int(0, 1));

            Assert.IsFalse(balancer.TickFlightRebalance(1f), "one alone: nothing to form on the first beat");

            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), null,
                new Vector2Int(2, 1));
            Assert.IsTrue(balancer.TickFlightRebalance(1f), "the second beat is a cycle");
            Assert.IsTrue(balancer.TickFlightRebalance(1f), "and the third, between cycles, forms the pair up");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        // Three toughs on row 0 of the 3 by 3 board: the wave start makes a snake of them, unless formAtOnce is
        // off, when it waits for the test to arrange.
        private BalloonBalancer BalancerWithAToughSnake(
            out UnbreakableFormationService formations, float toughStepSeconds = 1f, bool formAtOnce = true)
        {
            var settings = Substitute.For<IUnbreakableFormationSettings>();
            settings.Enabled.Returns(true);
            settings.ToughSnakes.Returns(true);
            settings.MinMembers.Returns(3);
            settings.ToughSnakeMinMembers.Returns(3);
            settings.MaxMembers.Returns(5);
            settings.BeatSeconds.Returns(1.7f);
            settings.BeatsPerCycle.Returns(2);
            settings.ToughSnakeStepSeconds.Returns(toughStepSeconds);
            _balloonsConfig.UnbreakableFormation.Returns(settings);
            formations = new UnbreakableFormationService(_grid, _balloonsConfig,
                new ThreatEase(Substitute.For<IPlayerNeed>(), Substitute.For<IAssistSettings>()));
            formations.Start();
            for (var col = 0; col < 3; col++)
            {
                _grid.Place(new ToughBalloonModel(new BalloonModelConfig(BalloonType.Tough, hitsToPop: col + 1)), null,
                    new Vector2Int(col, 0));
            }

            settings.ToughSnakes.Returns(formAtOnce);
            formations.ArrangeOnCycle(_ => { });
            settings.ToughSnakes.Returns(true);
            Assert.AreEqual(formAtOnce ? 1 : 0, formations.Formations.Count);

            var pathHolder = new BalancePathHolder();
            return new BalloonBalancer(
                _grid, new GridBalanceQuery(_grid), _balloonsConfig, pathHolder, CaptureBalanceRequests(),
                _registry, _pauseService, null, new BalloonMotionTicker(), formations: formations);
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
