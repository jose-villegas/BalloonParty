using System;
using System.Collections.Generic;
using BalloonParty.Editor.ShotSolver;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared;
using BalloonParty.Slots.Grid;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.ShotSolver
{
    [TestFixture]
    public class ShotSimulatorTests
    {
        // A 10-wide box centred on the origin: top +5, right +5, bottom −5, left −5 (clockwise
        // convention x=top, y=right, z=bottom, w=left) — same convention as ProjectileMotionResolverTests.
        private static readonly Vector4 WideOpenWalls = new(1000f, 1000f, -1000f, -1000f);

        [Test]
        public void Simulate_ColumnOfThreeGreens_ScoresStreak1Plus2Plus3()
        {
            var board = new[]
            {
                new ShotBalloonSnapshot(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 3f), 0.1f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(1 + 2 + 3, result.RawScore, "streak multiplier climbs 1, 2, 3 through the column");
            Assert.AreEqual(3, result.Pops);
            Assert.AreEqual(0, result.ToughsCleared);
            Assert.IsTrue(result.BoardCleared);
            Assert.IsFalse(result.Died);
        }

        [Test]
        public void Simulate_WallBankShot_OneBounceReachesGreen()
        {
            // 10-wide box; firing straight along +X bounces head-on off the right wall (x=5) back
            // along -X, reaching a balloon sitting on the far (negative-X) side — the unfolded-wall
            // shot the plan's §2 "walls unfold" intuition describes.
            var walls = new Vector4(5f, 5f, -5f, -5f);
            var board = new[] { new ShotBalloonSnapshot(new Vector2(-3f, 0f), 0.3f, "Blue", 5, 1) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(5, result.RawScore, "single green, first pop of its colour — multiplier 1");
            Assert.AreEqual(1, result.Pops);
            Assert.AreEqual(2, result.Events, "one wall bounce then one pop");
            Assert.IsTrue(result.BoardCleared);
            Assert.IsFalse(result.Died, "one shield was enough for the single bounce");
        }

        [Test]
        public void Simulate_ToughTwoTouch_DeflectsThenPopsWithStreakReset()
        {
            // Fired dead-centre at a two-hit tough: the first contact deflects it straight back down
            // a shield-costing bottom-wall bounce sends it straight back up onto the SAME tough for the
            // second (fatal) contact, which pops it via the flat/streak-reset tough rule.
            var walls = new Vector4(1000f, 1000f, -1f, -1000f);
            var board = new[] { new ShotBalloonSnapshot(new Vector2(0f, 2f), 0.3f, null, 4, 2) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.AreEqual(4, result.RawScore, "tough pops flat ScoreValue regardless of streak");
            Assert.AreEqual(1, result.Pops);
            Assert.AreEqual(1, result.ToughsCleared);
            Assert.AreEqual(3, result.Events, "deflect, wall bounce, pop");
            Assert.IsTrue(result.BoardCleared);
            Assert.IsFalse(result.Died);
        }

        [Test]
        public void Simulate_WallBouncesExceedShieldBudget_Dies()
        {
            // A balloon far off the horizontal bounce corridor keeps the board non-empty (the solver
            // stops early once the board clears) without ever being reachable, so the death comes
            // purely from consecutive wall bounces outrunning the shield budget.
            var walls = new Vector4(1000f, 1f, -1000f, -1f);
            var board = new[] { new ShotBalloonSnapshot(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            Assert.IsTrue(result.Died, "second bounce drops shields below zero");
            Assert.AreEqual(2, result.Events);
            Assert.AreEqual(0, result.Pops);
            Assert.IsFalse(result.BoardCleared, "the off-path filler balloon was never reached");
        }

        [Test]
        public void Simulate_SameColorStreakOfTwo_RefundsShieldAndSurvivesBounce()
        {
            // No shields to spare: a same-colour double pop (streak reaches 2) must refund one before
            // the shot reaches the (nearby) top wall, or it dies there instead of surviving the bounce.
            // A third balloon sits on the post-bounce return path so the board clears right after the
            // bounce is resolved, rather than the shot bouncing indefinitely inside the box.
            var walls = new Vector4(3f, 1000f, -1000f, -1000f);
            var refundingBoard = new[]
            {
                new ShotBalloonSnapshot(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, -0.5f), 0.1f, "Green", 1, 1), // on the return path
            };
            var refundingWorkingSet = new ShotBalloonState[refundingBoard.Length];

            var refundingResult = ShotSimulator.Simulate(
                refundingBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: refundingWorkingSet);

            Assert.IsFalse(refundingResult.Died, "the streak-of-two refund covers the top-wall bounce");
            Assert.IsTrue(refundingResult.BoardCleared);

            var nonRefundingBoard = new[]
            {
                new ShotBalloonSnapshot(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 2f), 0.1f, "Blue", 1, 1), // breaks the streak
                new ShotBalloonSnapshot(new Vector2(0f, -0.5f), 0.1f, "Green", 1, 1),
            };
            var nonRefundingWorkingSet = new ShotBalloonState[nonRefundingBoard.Length];

            var nonRefundingResult = ShotSimulator.Simulate(
                nonRefundingBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: nonRefundingWorkingSet);

            Assert.IsTrue(nonRefundingResult.Died, "without a same-colour streak of two, no refund covers the bounce");
        }

        [Test]
        public void Simulate_Timestamps_MatchDistanceOverSpeed()
        {
            // A pure ping-pong corridor at speed 2: wall at x=1 after 1 unit (t=0.5), then the far
            // wall at x=-1 after 2 more units (t=1.5), where the shot dies. The filler balloon keeps
            // the board non-empty without ever being reachable.
            var walls = new Vector4(1000f, 1f, -1000f, -1f);
            var board = new[] { new ShotBalloonSnapshot(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 2f, timestampsOut: timestamps);

            Assert.IsTrue(result.Died);
            Assert.AreEqual(3, timestamps.Count, "origin + two wall events");
            Assert.AreEqual(0f, timestamps[0], 1e-4f);
            Assert.AreEqual(0.5f, timestamps[1], 1e-4f);
            Assert.AreEqual(1.5f, timestamps[2], 1e-4f);
        }

        [Test]
        public void Simulate_CruiseRamp_AcceleratesTimelineBounceToBounce()
        {
            // Same corridor at base speed 1, threshold 1, 1.0/shield, linear curve. Bounce 1 (t=1)
            // enters cruise with 1 shield banked at BASE speed (progress 0); the bounce that spends
            // it lifts progress to 1, so the final wall-to-wall crossing runs at 1 + 1x1 = x2 speed:
            // timestamps 0, 1, 3 (still x1), 4 (2 units at x2).
            var walls = new Vector4(1000f, 1f, -1000f, -1f);
            var board = new[] { new ShotBalloonSnapshot(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();
            var cruise = new ShotCruiseConfig(
                wallBounceThreshold: 1, speedPerShield: 1f, rampCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 2, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f, cruiseConfig: cruise, timestampsOut: timestamps);

            Assert.IsTrue(result.Died);
            Assert.AreEqual(4, timestamps.Count);
            Assert.AreEqual(1f, timestamps[1], 1e-4f, "first bounce at base speed");
            Assert.AreEqual(3f, timestamps[2], 1e-4f, "cruise entered but no shield spent yet — still base speed");
            Assert.AreEqual(4f, timestamps[3], 1e-4f, "one banked shield spent — full x2 for the last crossing");
        }

        [Test]
        public void Simulate_CruiseLookahead_BalloonInCorridorBlocksEntry()
        {
            // Identical corridor, but a balloon sits ON the ping-pong line: the lookahead sees it, so
            // cruise never engages and every crossing stays at base speed — timing proves it, and the
            // shot pops the blocker on the way (contact also resets the bounce counter).
            var walls = new Vector4(1000f, 1f, -1000f, -1f);
            var board = new[]
            {
                new ShotBalloonSnapshot(new Vector2(-0.5f, 0f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 500f), 0.2f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();
            var cruise = new ShotCruiseConfig(
                wallBounceThreshold: 1, speedPerShield: 1f, rampCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 2, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f, cruiseConfig: cruise, timestampsOut: timestamps);

            Assert.IsTrue(result.Died);
            Assert.AreEqual(1, result.Pops, "the corridor blocker is popped en route");

            // Events: wall x=1 (t=1); lookahead toward x=-1 sees the blocker -> no cruise. Pop at
            // x=-0.4 (t=2.4). Wall x=-1 (t=3.0) — counter restarted by the contact, lookahead now
            // clear -> cruise enters with 0 shields banked = x1 speed cap, so the last crossing to
            // x=1 still takes 2s (t=5.0), where the shot dies.
            Assert.AreEqual(5, timestamps.Count);
            Assert.AreEqual(2.4f, timestamps[2], 1e-4f, "base speed to the blocker — no premature boost");
            Assert.AreEqual(5f, timestamps[4], 1e-4f, "zero shields banked at entry — cap stays x1");
        }

        [Test]
        public void Simulate_WithTimelineDefaults_MatchesStaticResults()
        {
            // The regression gate for tasks 4b/4c: the timeline/cruise/dynamics parameters must not
            // perturb outcomes — same geometry in, same score out, at any speed, with cruise armed
            // but never triggerable (threshold higher than the flight's bounce count) and no dynamics.
            var board = new[]
            {
                new ShotBalloonSnapshot(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                new ShotBalloonSnapshot(new Vector2(0f, 3f), 0.1f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];
            var cruise = new ShotCruiseConfig(
                wallBounceThreshold: 99, speedPerShield: 5f, rampCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f));

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 7f, cruiseConfig: cruise);

            Assert.AreEqual(1 + 2 + 3, result.RawScore);
            Assert.AreEqual(3, result.Pops);
            Assert.IsTrue(result.BoardCleared);
        }

        [Test]
        public void Reach_MatchesBalloonMotionTickerEnvelope()
        {
            // Out-and-back: ease-out-quad up to 1 at half duration, mirrored back down to 0.
            Assert.AreEqual(0f, ShotMotionMath.Reach(0f), 1e-5f);
            Assert.AreEqual(0.75f, ShotMotionMath.Reach(0.25f), 1e-5f, "EaseOutQuad(0.5) on the way out");
            Assert.AreEqual(1f, ShotMotionMath.Reach(0.5f), 1e-5f, "peak displacement at half duration");
            Assert.AreEqual(0.25f, ShotMotionMath.Reach(0.75f), 1e-5f, "mirrored on the way back");
            Assert.AreEqual(0f, ShotMotionMath.Reach(1f), 1e-5f);
        }

        [Test]
        public void TrySolveMovingEntry_StationaryTarget_ReducesToStaticLineCircle()
        {
            var found = ShotMotionMath.TrySolveMovingEntry(
                Vector2.zero, Vector2.right, speed: 3f, center: new Vector2(3f, 0f), velocity: Vector2.zero,
                combinedRadius: 0.5f, out var distance);

            Assert.IsTrue(found);
            Assert.AreEqual(2.5f, distance, 1e-4f, "plain head-on entry at center minus radius");
        }

        [Test]
        public void TrySolveMovingEntry_HeadOnCloser_MeetsAtRelativeSpeed()
        {
            // Projectile at speed 1 along +X, balloon closing at 1 along -X from x=5: relative closing
            // rate 2 per unit of projectile travel, so entry (gap 5 minus radius 0.5) at distance 2.25.
            var found = ShotMotionMath.TrySolveMovingEntry(
                Vector2.zero, Vector2.right, speed: 1f, center: new Vector2(5f, 0f), velocity: new Vector2(-1f, 0f),
                combinedRadius: 0.5f, out var distance);

            Assert.IsTrue(found);
            Assert.AreEqual(2.25f, distance, 1e-4f);
        }

        [Test]
        public void TrySolveMovingEntry_TargetOutrunsShot_NoEntry()
        {
            // The balloon flees along +X faster than the shot travels — the gap only grows.
            var found = ShotMotionMath.TrySolveMovingEntry(
                Vector2.zero, Vector2.right, speed: 1f, center: new Vector2(3f, 0f), velocity: new Vector2(2f, 0f),
                combinedRadius: 0.5f, out _);

            Assert.IsFalse(found);
        }

        [Test]
        public void EvaluateBalancePosition_MirrorsDOTweenOutQuadEase()
        {
            // The live tween is DOPath with the project's DOTween default ease (OutQuad, per
            // DOTweenSettings.asset): at half the duration the balloon has covered 75% of its path.
            var actor = new ShotSimDynamicActor();
            actor.ResetTo(Vector2Int.zero, Vector2.zero);
            actor.BeginBalanceMove(startTime: 0f, toPosition: new Vector2(10f, 0f), duration: 1f);

            Assert.AreEqual(0f, actor.EvaluateBalancePosition(0f).x, 1e-4f);
            Assert.AreEqual(7.5f, actor.EvaluateBalancePosition(0.5f).x, 1e-4f, "OutQuad(0.5) = 0.75");
            Assert.AreEqual(10f, actor.EvaluateBalancePosition(1f).x, 1e-4f);
            Assert.AreEqual(10f, actor.EvaluateBalancePosition(5f).x, 1e-4f, "settled — holds the target");
            Assert.AreEqual(Vector2.zero, actor.EvaluateBalanceVelocity(5f), "no velocity after settling");
        }

        [Test]
        public void BeginBalanceMove_SamePulseHops_ChainAsArcLengthPolyline()
        {
            // Two same-pulse hops (right 1, then up 1) form an L-shaped path of length 2, walked by
            // eased ARC LENGTH like DOPath's constant-speed percentage: OutQuad(0.5) = 0.75 of the
            // path = 1.5 units in — halfway up the vertical leg, NOT on the straight chord to (1,1).
            var actor = new ShotSimDynamicActor();
            actor.ResetTo(Vector2Int.zero, Vector2.zero);
            actor.BeginBalanceMove(startTime: 0f, toPosition: new Vector2(1f, 0f), duration: 1f);
            actor.BeginBalanceMove(startTime: 0f, toPosition: new Vector2(1f, 1f), duration: 1f);

            var midway = actor.EvaluateBalancePosition(0.5f);

            Assert.AreEqual(1f, midway.x, 1e-4f);
            Assert.AreEqual(0.5f, midway.y, 1e-4f);
        }

        [Test]
        public void Simulate_BalancePulse_MovesHangingBalloonIntoTheShotsPath()
        {
            // A 1x2 grid: the only balloon hangs at row 1 over an empty row 0 (unbalanced by
            // definition), 0.5 off the shot's line thanks to the odd-row hex stagger. Statically the
            // shot flies past; with dynamics, the first rebalance pulse (t=1) drops the balloon to
            // row 0 (settled by t=1.1) squarely onto the flight line, and the slow shot arrives later.
            var separation = new Vector2(1f, 1f);
            var offset = new Vector2(0f, -4f);
            var slot0 = (Vector2)HexCoordinates.IndexToWorldPosition(new Vector2Int(0, 0), separation, offset);
            var slot1 = (Vector2)HexCoordinates.IndexToWorldPosition(new Vector2Int(0, 1), separation, offset);
            Assert.AreNotEqual(slot0.x, slot1.x, "sanity: the hex stagger must offset the rows horizontally");

            var gameConfig = Substitute.For<IGameConfiguration>();
            gameConfig.SlotsSize.Returns(new Vector2Int(1, 2));
            gameConfig.SlotSeparation.Returns(separation);
            gameConfig.SlotsOffset.Returns(offset);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(1f);
            balloonsConfig.TimeForBalloonsBalance.Returns(0.1f);

            var board = new[]
            {
                new ShotBalloonSnapshot(
                    slot1, 0.2f, "Red", 1, 1,
                    slotIndex: new Vector2Int(0, 1), balancePriority: 0, maxBalanceSteps: 0,
                    directBalanceMotion: false, nudgeOverrides: null),
            };
            var dynamics = new ShotBoardDynamics(
                gameConfig, balloonsConfig, board,
                Array.Empty<ShotDynamicActorSnapshot>(), Array.Empty<ShotStaticActorSnapshot>());
            var workingSet = new ShotBalloonState[board.Length];

            var origin = new Vector2(slot0.x, slot0.y - 5f);
            var walls = new Vector4(1000f, 1000f, -1000f, -1000f);

            var staticResult = ShotSimulator.Simulate(
                board, walls, origin, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f);
            Assert.AreEqual(0, staticResult.Pops, "statically the hanging balloon is 0.5 off the line — a miss");

            var dynamicResult = ShotSimulator.Simulate(
                board, walls, origin, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f, dynamics: dynamics);
            Assert.AreEqual(1, dynamicResult.Pops, "the rebalance pulse drops it onto the flight line in time");
            Assert.IsTrue(dynamicResult.BoardCleared);
        }
    }
}
