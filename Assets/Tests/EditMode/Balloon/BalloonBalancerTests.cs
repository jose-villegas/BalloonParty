using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Formation;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Game.Assist;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Animation;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
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
        private readonly List<GameObject> _viewObjects = new();
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
            foreach (var viewObject in _viewObjects)
            {
                UnityEngine.Object.DestroyImmediate(viewObject);
            }

            _viewObjects.Clear();
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
        public void HasPossibleMove_ABalloonACoverHoldsOverAGap_False_UntilItIsLetGo()
        {
            var balloon = new BalloonModel();
            balloon.SetHeldByCover(true);
            _grid.Place(balloon, null, new Vector2Int(1, 1));

            Assert.IsFalse(_balancer.HasPossibleMove(), "a covered balloon holds its slot, so it is no move");
            Assert.IsFalse(_balancer.TickFlightRebalance(5f), "and the flight pulse does not fire for it");

            balloon.SetHeldByCover(false);
            Assert.IsTrue(_balancer.HasPossibleMove());
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
        public void FreeUnbreakables_FormUpOnACycle_NotOnTheBeatBetween()
        {
            var balancer = BalancerWithAToughSnake(out var formations, toughStepSeconds: 100f, formAtOnce: false);
            _balloonsConfig.UnbreakableFormation.ToughSnakes.Returns(false);
            _balloonsConfig.UnbreakableFormation.BeatSeconds.Returns(1f);
            _balloonsConfig.UnbreakableFormation.MinMembers.Returns(2);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), null,
                new Vector2Int(0, 1));
            _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), null,
                new Vector2Int(2, 1));

            Assert.IsFalse(balancer.TickFlightRebalance(1f), "two free, but the first beat is between cycles");
            Assert.IsTrue(balancer.TickFlightRebalance(1f), "the second beat is a cycle");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void AnUnbreakableSnake_StepsOnTheBeatAndTheOffBeat_TheCycleKeepsTheBeat()
        {
            var balancer = BalancerWithAToughSnake(out var formations, toughStepSeconds: 100f, formAtOnce: false);
            var settings = _balloonsConfig.UnbreakableFormation;
            settings.ToughSnakes.Returns(false);
            settings.BeatSeconds.Returns(1f);
            settings.SnakeStepsPerBeat.Returns(2);
            settings.HorizontalWeight.Returns(0f);
            settings.SnakeWeight.Returns(1f);
            settings.SnakeMaxMembers.Returns(3);
            settings.HorizontalMaxMembers.Returns(3);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            for (var col = 0; col < 3; col++)
            {
                _grid.Remove(new Vector2Int(col, 0));
                _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)), null,
                    new Vector2Int(col, 0));
            }

            formations.ArrangeOnCycle(_ => { });
            Assert.IsTrue(formations.HasUnbreakableSnake);

            Assert.IsTrue(balancer.TickFlightRebalance(0.5f), "a step on the off-beat, half a beat in");
            Assert.IsTrue(balancer.TickFlightRebalance(0.5f), "and on the beat");
            Assert.IsFalse(balancer.TickFlightRebalance(0.4f));
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ARing_FallsDueOnTheBeat_NotOnTheOffBeatStep()
        {
            var balancer = BalancerWithARing(out var formations, out _);

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "half a beat in: a snake would step, a ring does not turn");
            Assert.IsTrue(balancer.TickFlightRebalance(0.5f), "the beat turns it");
            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "the off-beat step again");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ARing_TurnsOneSlotOnTheBeat_ThroughADoomedGlide()
        {
            var balancer = BalancerWithARing(out var formations, out var ring);
            var before = ring.MemberList.Select(m => m.SlotIndex.Value).ToList();
            var shot = LoadFreeProjectile();
            shot.IsLastShieldApproach.Value = true;

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "the doomed glide holds the board");
            CollectionAssert.AreEqual(before, ring.MemberList.Select(m => m.SlotIndex.Value).ToList(), "off the beat");

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f));

            var centre = ring.TurnCentre;
            for (var i = 0; i < before.Count; i++)
            {
                var direction = HexCoordinates.DirectionBetween(centre, before[i]);
                Assert.AreEqual(
                    HexCoordinates.Step(centre, direction + 1), ring.MemberList[i].SlotIndex.Value,
                    "each member a slot clockwise on the beat");
            }

            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ASpinner_FallsDueOnTheBeat_LikeARing_NotOnTheOffBeatStep()
        {
            var balancer = BalancerWithASpinner(out var formations, out _);

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "half a beat in: a snake would step, a spinner does not turn");
            Assert.IsTrue(balancer.TickFlightRebalance(0.5f), "the beat turns it");
            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "the off-beat step again");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void ASpinner_TurnsSixtyDegreesOnTheBeat_ThroughADoomedGlide_WithTheMiddleInPlace()
        {
            var balancer = BalancerWithASpinner(out var formations, out var spinner);
            var before = spinner.MemberList.Select(m => m.SlotIndex.Value).ToList();
            var shot = LoadFreeProjectile();
            shot.IsLastShieldApproach.Value = true;

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "the doomed glide holds the board");
            CollectionAssert.AreEqual(before, spinner.MemberList.Select(m => m.SlotIndex.Value).ToList(), "off the beat");

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f));

            var centre = spinner.TurnCentre;
            for (var i = 0; i < before.Count; i++)
            {
                var after = spinner.MemberList[i].SlotIndex.Value;
                if (before[i] == centre)
                {
                    Assert.AreEqual(centre, after, "the middle member stays");
                    continue;
                }

                var direction = HexCoordinates.DirectionBetween(centre, before[i]);
                Assert.AreEqual(HexCoordinates.Step(centre, direction + 1), after, "each other member 60 degrees clockwise");
            }

            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [Test]
        public void AnUnbreakableSnake_StepsOnItsTick_ThroughADoomedGlide()
        {
            // The rhythm comes first: the glide holds the rest of the board, not the snake.
            var balancer = BalancerWithAToughSnake(out var formations, toughStepSeconds: 100f, formAtOnce: false);
            var settings = _balloonsConfig.UnbreakableFormation;
            settings.ToughSnakes.Returns(false);
            settings.BeatSeconds.Returns(1f);
            settings.SnakeStepsPerBeat.Returns(2);
            settings.HorizontalWeight.Returns(0f);
            settings.SnakeWeight.Returns(1f);
            settings.SnakeMaxMembers.Returns(3);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            for (var col = 0; col < 3; col++)
            {
                var slot = new Vector2Int(col, 0);
                _grid.Remove(slot);
                _grid.Place(new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                    ViewObject(), slot);
            }

            formations.ArrangeOnCycle(_ => { });
            var snake = formations.Formations.Single(f => f.Shape == FormationShape.Snake);
            var before = snake.Members.Select(m => m.SlotIndex.Value).ToList();
            var shot = LoadFreeProjectile();
            shot.IsLastShieldApproach.Value = true;

            Assert.IsFalse(balancer.TickFlightRebalance(0.5f), "the doomed glide holds the board");
            Assert.AreNotEqual(before[0], snake.Members[0].SlotIndex.Value, "but the snake steps on its tick");
            Assert.AreEqual(before[0], snake.Members[1].SlotIndex.Value, "the member behind takes the head's slot");
            formations.Dispose();
            balancer.ResetRun(balancer.Generation + 1);
        }

        [TestCase(0, 0f, 0.25f, 0)]
        [TestCase(0, 0.24f, 0.25f, 0)]
        [TestCase(0, 0.25f, 0.25f, 1)]
        [TestCase(0, 0.9f, 0.25f, 1)]
        [TestCase(1, 0f, 0.25f, 1)]
        [TestCase(1, 0.3f, 0.25f, 2)]
        [TestCase(4, 0.1f, 0.25f, 4)]
        [TestCase(4, 0.5f, 0.25f, 5)]
        [TestCase(3, 0f, 0f, 4)]
        public void OffBeatsAt_CountsOneAShareOfABeatAfterTheStartAndAfterEachBeat(
            int beats, float phase, float share, int offBeats)
        {
            Assert.AreEqual(offBeats, BalloonBalancer.OffBeatsAt(beats, phase, share));
        }

        // A beat of 1 s with two snake steps in it, stepped an eighth of a second at a time, on a board with no
        // unbreakable, no tough and no snake: the off-beat falls a quarter beat into the run and a quarter beat after
        // each beat, and the count never falls or jumps.
        [Test]
        public void TheOffBeat_IsCountedOnAnEmptyBoard_AQuarterBeatAfterEachBeat()
        {
            var balancer = BalancerWithABeat(out var formations);
            IBoardBeat beat = balancer;
            var counts = new List<int>();

            for (var step = 1; step <= 18; step++)
            {
                Assert.IsFalse(balancer.TickFlightRebalance(0.125f), "nothing on the board is due");
                counts.Add(beat.OffBeats(0.25f));
            }

            // Seconds of play:      .125 .25 .375 .5 .625 .75 .875 1  1.125 1.25 ... 2  2.125 2.25
            CollectionAssert.AreEqual(
                new[] { 0, 1, 1, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2, 2, 2, 2, 3 }, counts);
            formations.Dispose();
        }

        [Test]
        public void TheOffBeat_ReadAtAnotherShare_FallsThere()
        {
            var balancer = BalancerWithABeat(out var formations);
            IBoardBeat beat = balancer;

            balancer.TickFlightRebalance(0.5f);
            Assert.AreEqual(0, beat.OffBeats(0.75f), "half a beat in");
            Assert.AreEqual(1, beat.OffBeats(0.5f));
            balancer.TickFlightRebalance(0.25f);

            Assert.AreEqual(1, beat.OffBeats(0.75f));
            formations.Dispose();
        }

        [Test]
        public void APause_HoldsTheOffBeat_AndItGoesOnFromWhereItStood()
        {
            var balancer = BalancerWithABeat(out var formations);
            IBoardBeat beat = balancer;
            balancer.TickFlightRebalance(0.125f);

            _pauseService.Pause(PauseSource.Cheat);
            for (var step = 0; step < 20; step++)
            {
                balancer.TickFlightRebalance(0.125f);
            }

            Assert.AreEqual(0, beat.OffBeats(0.25f), "no beat passes in a pause");

            _pauseService.Resume(PauseSource.Cheat);
            balancer.TickFlightRebalance(0.125f);

            Assert.AreEqual(1, beat.OffBeats(0.25f), "a quarter beat of play in all");
            formations.Dispose();
        }

        // The rhythm comes first: a doomed shot's last glide holds the board, not the beat.
        [Test]
        public void ADoomedGlide_DoesNotHoldTheOffBeat()
        {
            var balancer = BalancerWithABeat(out var formations);
            IBoardBeat beat = balancer;
            LoadFreeProjectile().IsLastShieldApproach.Value = true;

            balancer.TickFlightRebalance(0.25f);

            Assert.AreEqual(1, beat.OffBeats(0.25f));
            formations.Dispose();
        }

        // With the pulse switched off the balancer moves nothing, but what reads the beat still gets it.
        [Test]
        public void WithTheFlightPulseOff_TheOffBeatIsStillCounted()
        {
            var balancer = BalancerWithABeat(out var formations);
            _balloonsConfig.FlightRebalanceInterval.Returns(0f);
            IBoardBeat beat = balancer;

            for (var step = 0; step < 10; step++)
            {
                Assert.IsFalse(balancer.TickFlightRebalance(0.125f));
            }

            Assert.AreEqual(2, beat.OffBeats(0.25f), "1.25 s of play: a quarter beat past the first beat");
            formations.Dispose();
        }

        [Test]
        public void ARunReset_StartsTheOffBeatCountOver()
        {
            var balancer = BalancerWithABeat(out var formations);
            IBoardBeat beat = balancer;
            for (var step = 0; step < 10; step++)
            {
                balancer.TickFlightRebalance(0.125f);
            }

            Assert.AreEqual(2, beat.OffBeats(0.25f));

            balancer.ResetRun(balancer.Generation + 1);

            Assert.AreEqual(0, beat.OffBeats(0.25f));
            formations.Dispose();
        }

        [Test]
        public void WithNoFormationService_ThereIsNoBeat()
        {
            IBoardBeat beat = _balancer;

            _balancer.TickFlightRebalance(0.5f);

            Assert.AreEqual(0, beat.OffBeats(0.25f));
        }

        // An empty board and a formation service with the unbreakables' clock set: a beat of 1 s, two snake steps a
        // beat, two beats a cycle. The flight pulse is 100 s away, so only the beat can make anything due.
        private BalloonBalancer BalancerWithABeat(out UnbreakableFormationService formations)
        {
            var settings = Substitute.For<IUnbreakableFormationSettings>();
            settings.Enabled.Returns(true);
            settings.MinMembers.Returns(2);
            settings.BeatSeconds.Returns(1f);
            settings.BeatsPerCycle.Returns(2);
            settings.SnakeStepsPerBeat.Returns(2);
            _balloonsConfig.UnbreakableFormation.Returns(settings);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            formations = new UnbreakableFormationService(_grid, _balloonsConfig,
                new ThreatEase(Substitute.For<IPlayerNeed>(), Substitute.For<IAssistSettings>()));
            formations.Start();
            return new BalloonBalancer(
                _grid, new GridBalanceQuery(_grid), _balloonsConfig, new BalancePathHolder(), CaptureBalanceRequests(),
                _registry, _pauseService, null, new BalloonMotionTicker(), formations: formations);
        }

        // A ring of four unbreakables round the middle slot of the 3 by 3 board (on its slots 4, 5, 0 and 1, which
        // stand clear of the west side so the ring never walls the board), turning clockwise twice a beat.
        private BalloonBalancer BalancerWithARing(out UnbreakableFormationService formations, out StandingFormation ring)
        {
            var balancer = BalancerWithAToughSnake(out formations, toughStepSeconds: 100f, formAtOnce: false);
            var settings = _balloonsConfig.UnbreakableFormation;
            settings.ToughSnakes.Returns(false);
            settings.BeatSeconds.Returns(1f);
            settings.SnakeStepsPerBeat.Returns(2);
            settings.RingWeight.Returns(1f);
            settings.RingMinMembers.Returns(4);
            settings.RingMaxMembers.Returns(5);
            settings.ReshapeEveryCycles.Returns(8);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            formations.Random01 = () => 0f;
            for (var col = 0; col < 3; col++)
            {
                _grid.Remove(new Vector2Int(col, 0));
            }

            var centre = new Vector2Int(1, 1);
            foreach (var direction in new[] { 4, 5, 0, 1 })
            {
                _grid.Place(
                    new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                    ViewObject(), HexCoordinates.Step(centre, direction));
            }

            formations.ArrangeOnCycle(_ => { });
            ring = formations.Formations.Single(f => f.Shape == FormationShape.Ring);
            Assert.AreEqual(centre, ring.TurnCentre);
            Assert.IsTrue(ring.Clockwise);
            return balancer;
        }

        // A spinner of three unbreakables about the middle slot of the 3 by 3 board, on the diagonal from its north-west
        // to its south-east neighbour (which never reaches the west column, so the spinner never walls the board),
        // turning clockwise on the beat.
        private BalloonBalancer BalancerWithASpinner(out UnbreakableFormationService formations, out StandingFormation spinner)
        {
            var balancer = BalancerWithAToughSnake(out formations, toughStepSeconds: 100f, formAtOnce: false);
            var settings = _balloonsConfig.UnbreakableFormation;
            settings.ToughSnakes.Returns(false);
            settings.BeatSeconds.Returns(1f);
            settings.SnakeStepsPerBeat.Returns(2);
            settings.SpinnerWeight.Returns(1f);
            settings.SpinnerMaxMembers.Returns(5);
            settings.ReshapeEveryCycles.Returns(8);
            _balloonsConfig.FlightRebalanceInterval.Returns(100f);
            formations.Random01 = () => 0f;
            for (var col = 0; col < 3; col++)
            {
                _grid.Remove(new Vector2Int(col, 0));
            }

            var centre = new Vector2Int(1, 1);
            foreach (var slot in new[] { HexCoordinates.Step(centre, 4), centre, HexCoordinates.Step(centre, 1) })
            {
                _grid.Place(
                    new UnbreakableBalloonModel(new BalloonModelConfig(typeName: BalloonType.Unbreakable)),
                    ViewObject(), slot);
            }

            formations.ArrangeOnCycle(_ => { });
            spinner = formations.Formations.Single(f => f.Shape == FormationShape.Spinner);
            Assert.AreEqual(centre, spinner.TurnCentre);
            Assert.IsTrue(spinner.Clockwise);
            return balancer;
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

            // Only a snake formed at once takes the run's first cycle, so a test that arranges its own starts on
            // that one, a reshape.
            if (formAtOnce)
            {
                formations.ArrangeOnCycle(_ => { });
            }

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

        // A stand-in view for a balloon a step moves: the grid's default positions are all the same point, so the
        // move settles at once.
        private ISlotActorView ViewObject()
        {
            var viewObject = new GameObject("BalanceTestView");
            _viewObjects.Add(viewObject);
            var view = Substitute.For<ISlotActorView>();
            view.transform.Returns(viewObject.transform);
            view.TweenTracker.Returns(viewObject.AddComponent<TweenTracker>());
            return view;
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
