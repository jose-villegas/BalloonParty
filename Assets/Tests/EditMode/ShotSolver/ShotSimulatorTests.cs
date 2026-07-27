using System;
using System.Collections.Generic;
using BalloonParty.Solver;
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
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 1, 1),
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
            var board = new[] { ShotBoardBuilder.Green(new Vector2(-3f, 0f), 0.3f, "Blue", 5, 1) };
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
            var board = new[] { ShotBoardBuilder.Tough(new Vector2(0f, 2f), 0.3f, 4, 2) };
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
            var board = new[] { ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
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
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, -0.5f), 0.1f, "Green", 1, 1), // on the return path
            };
            var refundingWorkingSet = new ShotBalloonState[refundingBoard.Length];

            var refundingResult = ShotSimulator.Simulate(
                refundingBoard, walls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: refundingWorkingSet);

            Assert.IsFalse(refundingResult.Died, "the streak-of-two refund covers the top-wall bounce");
            Assert.IsTrue(refundingResult.BoardCleared);

            var nonRefundingBoard = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Blue", 1, 1), // breaks the streak
                ShotBoardBuilder.Green(new Vector2(0f, -0.5f), 0.1f, "Green", 1, 1),
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
            var board = new[] { ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
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
            var board = new[] { ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();
            var cruise = new ShotCruiseConfig(wallBounceThreshold: 1, speedGainPerTap: 1f);

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
        public void Simulate_ArmedShotKeepsTapping_LegsKeepShortening()
        {
            // An armed shot goes on earning taps (José, 2026-07-27), so its speed keeps climbing past the
            // arming bounce. A vertical corridor of equal-length legs makes that visible as timing alone:
            // every leg after the arming one is SHORTER than the last, at x4, x5, x6 and so on. The speed
            // rail (MaxSpeedMultiplier) is what eventually bounds this, not the arming tap.
            var walls = new Vector4(2f, 0.5f, -2f, -0.5f);

            // Far off the corridor: never hit, but a non-empty board keeps the flight going to the end of
            // its shields instead of stopping the moment the board reads as cleared.
            var board = new[] { ShotBoardBuilder.Green(new Vector2(100f, 100f), 0.05f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();
            var cruise = new ShotCruiseConfig(wallBounceThreshold: 1, speedGainPerTap: 1f, piercingTapThreshold: 2);

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 6, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f, cruiseConfig: cruise, timestampsOut: timestamps);

            Assert.IsTrue(result.Died);
            Assert.AreEqual(8, timestamps.Count);

            // [1] first wall = cruise entry (0 taps spent, so [1]->[2] is 4 units at base speed) ->
            // [2] one tap, x2 -> [3] the second tap, which arms piercing -> every leg from there at x3.
            Assert.AreEqual(4f, timestamps[2] - timestamps[1], 1e-4f, "cruise entered, no tap spent yet");
            Assert.AreEqual(2f, timestamps[3] - timestamps[2], 1e-4f, "one tap -> x2");
            Assert.AreEqual(4f / 3f, timestamps[4] - timestamps[3], 1e-4f, "armed at the second tap -> x3");
            Assert.AreEqual(1f, timestamps[5] - timestamps[4], 1e-4f, "a third tap while armed -> x4");
            Assert.AreEqual(0.8f, timestamps[6] - timestamps[5], 1e-4f, "and a fourth -> x5");
            Assert.Less(
                timestamps[7] - timestamps[6], timestamps[6] - timestamps[5],
                "each armed tap shortens the next leg — only the speed rail ever stops this");
        }

        [Test]
        public void Simulate_PierceArmRamp_CostsTheArmingBounceTimeInProportionToTheSpeedGap()
        {
            // An ARMED tap hands its transition to the ramp rather than the per-tap beat: it accelerates
            // from the speed the shot already has into the new target. On the event timeline that ramp is a
            // pure time cost, and only the part the remaining speed gap accounts for. Here the shot arms on
            // its second tap, ramping x2 -> x3 over 1s with a linear curve (mean 0.5) ⇒
            // 1 x 0.5 x (1 - 2/3) = 1/6s charged at that bounce, with every later event carrying it — and
            // each armed tap after it charging its own, smaller share.
            var walls = new Vector4(2f, 0.5f, -2f, -0.5f);
            var board = new[] { ShotBoardBuilder.Green(new Vector2(100f, 100f), 0.05f, "Red", 1, 1) };
            var linear = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            var withRamp = new ShotCruiseConfig(
                wallBounceThreshold: 1, speedGainPerTap: 1f, piercingTapThreshold: 2, armRampDuration: 1f,
                armRampCurve: linear);
            var noRamp = new ShotCruiseConfig(
                wallBounceThreshold: 1, speedGainPerTap: 1f, piercingTapThreshold: 2);

            var rampTimestamps = new List<float>();
            ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 6, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], projectileSpeed: 1f, cruiseConfig: withRamp,
                timestampsOut: rampTimestamps);

            var plainTimestamps = new List<float>();
            ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.up, startingShields: 6, projectileContactRadius: 0f,
                workingSet: new ShotBalloonState[board.Length], projectileSpeed: 1f, cruiseConfig: noRamp,
                timestampsOut: plainTimestamps);

            Assert.AreEqual(plainTimestamps.Count, rampTimestamps.Count, "the ramp costs time, never a path");
            Assert.AreEqual(
                plainTimestamps[2], rampTimestamps[2], 1e-4f, "bounces before the arming one are unaffected");

            // The arming bounce's OWN timestamp is recorded before HandleWallBounce charges the lag
            // (Simulate appends the event, then resolves the bounce), so the cost lands on everything
            // after it rather than on the arming event itself.
            Assert.AreEqual(
                plainTimestamps[3], rampTimestamps[3], 1e-4f, "the arming event itself is stamped pre-lag");
            Assert.AreEqual(
                1f / 6f, rampTimestamps[4] - plainTimestamps[4], 1e-3f,
                "the first leg after arming carries the ramp's cost");
            // NOT a one-off: taps keep accruing while armed, so every armed tap re-anchors the ramp and
            // pays its own share — each smaller than the last as the relative gap closes, since the lag is
            // ArmRampLagSeconds x (1 - from/to). By event 7 the shot has taken four of them: the arming tap
            // (x2 -> x3) plus three more (x3 -> x4, x4 -> x5, x5 -> x6).
            Assert.AreEqual(
                (1f / 6f) + 0.125f + 0.1f + (1f / 12f), rampTimestamps[7] - plainTimestamps[7], 1e-3f,
                "every armed tap pays its own ramp, so the offset accumulates down the flight");
        }

        // Taps are COUNTED now, not derived from shields spent since cruise entry. The derivation was
        // pinned to "a wall hit is the only way to lose a shield", and it made the sim drop to base speed
        // the moment a pop ended the cruise — where live keeps the speed its taps bought
        // (ResolveFlightSpeed reads the tap count, never IsCruising).
        //
        // Geometry: a narrow tall box with a 3-4-5 diagonal, so x-bounces bank taps while the shot climbs.
        // Bounces land at (1,1.333) t=1.667 [cruise entry], (-1,4) t=5 [tap 1], (1,6.667) t=6.667 [tap 2,
        // so the shot is now at x3], then a balloon sitting mid-leg at (0,8) pops.
        [Test]
        public void Simulate_PopEndsTheCruise_ButTheTapsItEarnedKeepTheirSpeed()
        {
            var walls = new Vector4(40f, 1f, -40f, -1f);
            var cruise = new ShotCruiseConfig(wallBounceThreshold: 1, speedGainPerTap: 1f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 8f), 0.05f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(500f, 500f), 0.05f, "Red", 1, 1),
            };
            var timestamps = new List<float>();

            var result = ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(0.6f, 0.8f), startingShields: 5,
                projectileContactRadius: 0f, workingSet: new ShotBalloonState[board.Length],
                projectileSpeed: 1f, cruiseConfig: cruise, timestampsOut: timestamps);

            Assert.AreEqual(1, result.Pops, "the mid-leg balloon pops");
            Assert.AreEqual(8, timestamps.Count);
            Assert.AreEqual(
                0.539f, timestamps[4] - timestamps[3], 1e-2f, "approach to the pop at the earned x3");
            Assert.AreEqual(
                0.572f, timestamps[5] - timestamps[4], 1e-2f,
                "and STILL x3 after it — the pop ends the cruise but not the taps; base speed would be ~1.72");
        }

        // E1b: Sweep taps in the sim. A sweep was worth nothing here, so the sim could never arm a pierce
        // through sweeps — and since piercing turns a tough contact from DEFLECT into plow-and-continue,
        // that was a trajectory divergence, not a timing one. Same board and aim, sweeps on vs off:
        //
        // Diagonal legs in a narrow tall box, one 1-HP balloon mid-leg on each of the first two legs (each
        // a clean sweep: a pop, all one-shot kills, corridor clear at the wall), then a 2-HP tough mid-leg
        // three. Two sweep taps arm the shot, so it plows the tough and carries on to the next wall; unarmed
        // it deflects off the tough and is sent back the way it came.
        [Test]
        public void Simulate_SweepTapsArmPiercing_ForkingTheTrajectoryAtATough()
        {
            var walls = new Vector4(40f, 1f, -40f, -1f);
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0.5f, 0.667f), 0.05f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2.667f), 0.05f, "Red", 1, 1),
                ShotBoardBuilder.Tough(new Vector2(0f, 5.333f), 0.05f, 5, 2),
                ShotBoardBuilder.Green(new Vector2(500f, 500f), 0.05f, "Red", 1, 1),
            };

            // WallBounceThreshold 99 keeps cruise out of it entirely, so every tap here is a sweep's.
            var sweptPath = new List<Vector2>();
            var swept = ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(0.6f, 0.8f), startingShields: 6,
                projectileContactRadius: 0f, workingSet: new ShotBalloonState[board.Length],
                projectileSpeed: 1f,
                cruiseConfig: new ShotCruiseConfig(
                    wallBounceThreshold: 99, speedGainPerTap: 1f, piercingTapThreshold: 2, sweepEnabled: true,
                    sweepTapThreshold: 1),
                pathOut: sweptPath);

            var controlPath = new List<Vector2>();
            var control = ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(0.6f, 0.8f), startingShields: 6,
                projectileContactRadius: 0f, workingSet: new ShotBalloonState[board.Length],
                projectileSpeed: 1f,
                cruiseConfig: new ShotCruiseConfig(
                    wallBounceThreshold: 99, speedGainPerTap: 1f, piercingTapThreshold: 2, sweepEnabled: false,
                    sweepTapThreshold: 1),
                pathOut: controlPath);

            Assert.AreEqual(3, swept.Pops, "armed by its sweeps, the shot plows the tough as a third pop");
            Assert.AreEqual(2, control.Pops, "never armed, it only pops the two 1-HP balloons");
            Assert.AreEqual(
                6.667f, sweptPath[6].y, 1e-2f, "the armed shot carries on THROUGH the tough to the next wall");
            Assert.Less(
                controlPath[6].y, 5f,
                "the unarmed one is deflected back down instead — the paths have forked, not just the clock");
        }

        [Test]
        public void Simulate_DeflectWipesTheTaps_UnlikeAPop()
        {
            // Same board with the blocker as a 2-HP tough, so it deflects instead of popping. Live treats
            // a deflect as interrupting the whole run (ProjectileView.OnBalloonDeflected zeroes the taps),
            // so the legs after it must run at BASE speed — identical to the same board with cruise
            // disabled entirely, which is what the control run pins.
            var walls = new Vector4(40f, 1f, -40f, -1f);
            var board = new[]
            {
                ShotBoardBuilder.Tough(new Vector2(0f, 8f), 0.05f, 5, 2),
                ShotBoardBuilder.Green(new Vector2(500f, 500f), 0.05f, "Red", 1, 1),
            };

            var cruised = new List<float>();
            ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(0.6f, 0.8f), startingShields: 5,
                projectileContactRadius: 0f, workingSet: new ShotBalloonState[board.Length],
                projectileSpeed: 1f, cruiseConfig: new ShotCruiseConfig(wallBounceThreshold: 1, speedGainPerTap: 1f),
                timestampsOut: cruised);

            var control = new List<float>();
            ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(0.6f, 0.8f), startingShields: 5,
                projectileContactRadius: 0f, workingSet: new ShotBalloonState[board.Length],
                projectileSpeed: 1f, cruiseConfig: new ShotCruiseConfig(wallBounceThreshold: 0, speedGainPerTap: 1f),
                timestampsOut: control);

            Assert.AreEqual(
                0.539f, cruised[4] - cruised[3], 1e-2f, "it reached the tough at the earned x3");
            Assert.AreEqual(
                control[5] - control[4], cruised[5] - cruised[4], 1e-3f,
                "the leg straight off the deflect is back to base speed — the taps were wiped, not kept");
            Assert.AreEqual(
                control[6] - control[5], cruised[6] - cruised[5], 1e-3f,
                "and so is the next one, up to cruise legitimately re-entering after it");
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
                ShotBoardBuilder.Green(new Vector2(-0.5f, 0f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.2f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();
            var cruise = new ShotCruiseConfig(wallBounceThreshold: 1, speedGainPerTap: 1f);

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
        public void Simulate_TapEaseLag_AddsTimePerCruiseBounce()
        {
            // Same corridor as the cruise-ramp test, plus a 1s tap animation with a linear 0->1 curve:
            // mean curve value 0.5, so EACH cruise bounce (entry at t=1, then the bounce at t=3.5)
            // adds 0.5s of timeline lag — timestamps shift from [0,1,3,4] to [0,1,3.5,5.0]: entry lag
            // pushes the base-speed crossing to 3.5, then the second lag plus 2 units at the x2
            // target (1s) lands the death bounce at 5.0.
            var walls = new Vector4(1000f, 1f, -1000f, -1f);
            var board = new[] { ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.2f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];
            var timestamps = new List<float>();
            var cruise = new ShotCruiseConfig(
                wallBounceThreshold: 1, speedGainPerTap: 1f, tapEaseDuration: 1f,
                tapCurve: AnimationCurve.Linear(0f, 0f, 1f, 1f));

            ShotSimulator.Simulate(
                board, walls, Vector2.zero, Vector2.right, startingShields: 2, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f, cruiseConfig: cruise, timestampsOut: timestamps);

            Assert.AreEqual(4, timestamps.Count);
            Assert.AreEqual(1f, timestamps[1], 1e-3f, "entry bounce lands on time; its lag applies after");
            Assert.AreEqual(3.5f, timestamps[2], 1e-3f, "0.5s entry-tap lag + the 2-unit crossing at base speed");
            Assert.AreEqual(5f, timestamps[3], 1e-3f, "second 0.5s tap lag + 2 units at the x2 target");
        }

        [Test]
        public void Simulate_WithTimelineDefaults_MatchesStaticResults()
        {
            // The regression gate for tasks 4b/4c: the timeline/cruise/dynamics parameters must not
            // perturb outcomes — same geometry in, same score out, at any speed, with cruise armed
            // but never triggerable (threshold higher than the flight's bounce count) and no dynamics.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];
            var cruise = new ShotCruiseConfig(wallBounceThreshold: 99, speedGainPerTap: 5f);

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
            actor.MoveSpeed = 10f; // path length 10 ÷ speed 10 = 1s duration
            actor.BeginBalanceMove(startTime: 0f, toPosition: new Vector2(10f, 0f));

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
            actor.MoveSpeed = 2f; // L-path length 2 ÷ speed 2 = 1s duration
            actor.BeginBalanceMove(startTime: 0f, toPosition: new Vector2(1f, 0f));
            actor.BeginBalanceMove(startTime: 0f, toPosition: new Vector2(1f, 1f));

            var midway = actor.EvaluateBalancePosition(0.5f);

            Assert.AreEqual(1f, midway.x, 1e-4f);
            Assert.AreEqual(0.5f, midway.y, 1e-4f);
        }

        [Test]
        public void BeginBalanceMove_MidWobble_SeedsPathFromTheWobbledCentre()
        {
            // The live tween's waypoint 0 is the view's CURRENT position, nudge offset included
            // (StartBalanceTween reads viewTransform.position; the motion ticker then re-adds the
            // impulse on top of every tween write). A pulse landing at an impulse's peak must
            // therefore start the path one full offset away from the lattice home.
            var actor = new ShotSimDynamicActor();
            actor.ResetTo(Vector2Int.zero, Vector2.zero);
            actor.NudgeImpulses.Add(new ShotNudgeImpulse
            {
                Offset = new Vector2(1f, 0f),
                StartTime = 0f,
                Duration = 1f,
            });

            // t = 0.5 is the Reach envelope's peak — the wobble is exactly the full offset.
            actor.MoveSpeed = 1f; // asserted at the path start, so the derived duration is irrelevant here
            actor.BeginBalanceMove(startTime: 0.5f, toPosition: new Vector2(0f, 5f));

            Assert.AreEqual(1f, actor.EvaluateBalancePosition(0.5f).x, 1e-4f,
                "path departs from the wobbled position, not the lattice home");
            Assert.AreEqual(2f, actor.EvaluateCenter(0.5f).x, 1e-4f,
                "the start offset is briefly double-carried — exactly what the live ticker does");
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

            var gameConfig = Substitute.For<ISlotGridConfig>();
            gameConfig.SlotsSize.Returns(new Vector2Int(1, 2));
            gameConfig.SlotSeparation.Returns(separation);
            gameConfig.SlotsOffset.Returns(offset);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(1f);

            // Speed chosen so a single-cell drop still takes ~0.1s (settled by t≈1.1), matching the
            // former fixed balance duration this test was written against.
            var balanceSpeed = Vector2.Distance(slot0, slot1) / 0.1f;
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    slot1, 0.2f, "Red", 1, 1,
                    slotIndex: new Vector2Int(0, 1), balancePriority: 0, maxBalanceSteps: 0,
                    moveSpeed: balanceSpeed, directBalanceMotion: false, nudgeOverrides: null),
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

        [Test]
        public void Simulate_PulseExecutionDelay_ShiftsWhenTheMoveStarts()
        {
            // Same hanging-balloon setup, but a pulse delay far longer than the flight: the move never
            // starts before the shot passes, so the dynamic run misses exactly like the static one —
            // proving the delay genuinely shifts the pulse schedule rather than being cosmetic.
            var separation = new Vector2(1f, 1f);
            var offset = new Vector2(0f, -4f);
            var slot0 = (Vector2)HexCoordinates.IndexToWorldPosition(new Vector2Int(0, 0), separation, offset);
            var slot1 = (Vector2)HexCoordinates.IndexToWorldPosition(new Vector2Int(0, 1), separation, offset);

            var gameConfig = Substitute.For<ISlotGridConfig>();
            gameConfig.SlotsSize.Returns(new Vector2Int(1, 2));
            gameConfig.SlotSeparation.Returns(separation);
            gameConfig.SlotsOffset.Returns(offset);

            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(1f);

            var balanceSpeed = Vector2.Distance(slot0, slot1) / 0.1f;
            var board = new[]
            {
                ShotBoardBuilder.Green(
                    slot1, 0.2f, "Red", 1, 1,
                    slotIndex: new Vector2Int(0, 1), balancePriority: 0, maxBalanceSteps: 0,
                    moveSpeed: balanceSpeed, directBalanceMotion: false, nudgeOverrides: null),
            };
            var delayedDynamics = new ShotBoardDynamics(
                gameConfig, balloonsConfig, board,
                Array.Empty<ShotDynamicActorSnapshot>(), Array.Empty<ShotStaticActorSnapshot>(),
                pulseExecutionDelay: 100f);
            var workingSet = new ShotBalloonState[board.Length];

            var result = ShotSimulator.Simulate(
                board, new Vector4(1000f, 1000f, -1000f, -1000f), new Vector2(slot0.x, slot0.y - 5f),
                Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, projectileSpeed: 1f, dynamics: delayedDynamics);

            Assert.AreEqual(0, result.Pops, "the delayed pulse never fires before the shot passes");
        }

        [Test]
        public void Simulate_PiercingArmedByCruiseTaps_PopsAWouldBeForeverDeflector()
        {
            // A narrow corridor climbed diagonally (slope 0.25: each 2-unit crossing rises 0.5).
            // Cruise enters at bounce 1 (threshold 1, the next segment is clear of the high target);
            // taps reach 3 at bounce 4, arming piercing just before segment 5 crosses the
            // 999-durability deflector at (0, 2) dead-on. Armed: it pops and flies on. Unarmed
            // control: the same flight only ever deflects it.
            var walls = new Vector4(1000f, 1f, -1000f, -1f);
            var board = new[]
            {
                ShotBoardBuilder.Tough(new Vector2(0f, 2f), 0.15f, 7, 999),
                ShotBoardBuilder.Green(new Vector2(0f, 500f), 0.2f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var armed = new ShotCruiseConfig(
                wallBounceThreshold: 1, speedGainPerTap: 0f, piercingTapThreshold: 3);
            var armedResult = ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(1f, 0.25f), startingShields: 6,
                projectileContactRadius: 0f, workingSet: workingSet, cruiseConfig: armed);

            Assert.AreEqual(1, armedResult.Pops, "armed at tap 3 — the deflector pops on contact");
            Assert.AreEqual(1, armedResult.ToughsCleared);
            Assert.AreEqual(7, armedResult.RawScore, "colourless pop scores its flat value");

            var unarmed = new ShotCruiseConfig(wallBounceThreshold: 1, speedGainPerTap: 0f);
            var unarmedResult = ShotSimulator.Simulate(
                board, walls, Vector2.zero, new Vector2(1f, 0.25f), startingShields: 6,
                projectileContactRadius: 0f, workingSet: workingSet, cruiseConfig: unarmed);

            Assert.AreEqual(0, unarmedResult.Pops, "without the piercing grant it only ever deflects");
        }

        [Test]
        public void Simulate_TargetColorFilter_ScopesScoreAttributionOnly()
        {
            // Red, Blue, Red column: filtered to "Red", only the two red pops score — but the streak
            // still runs unfiltered, so the second red lands at streak 3 (1 + 3 = 4), not streak 2.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var filtered = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, targetColorId: "Blue");

            Assert.AreEqual(0, filtered.RawScore, "no Blue on the board — nothing attributes");
            Assert.AreEqual(3, filtered.Pops, "pops still happen, they just don't score");

            var matching = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, targetColorId: "Red");

            Assert.AreEqual(1 + 2 + 3, matching.RawScore, "matching colour attributes with its true streaks");
        }

        [Test]
        public void Simulate_RadiusBias_TurnsANearMissIntoAHit()
        {
            // The ray passes 0.15 from a radius-0.1 balloon: a miss — until the +0.1 robustness bias
            // fattens the contact circle past the gap.
            var board = new[] { ShotBoardBuilder.Green(new Vector2(0.15f, 2f), 0.1f, "Red", 1, 1) };
            var workingSet = new ShotBalloonState[board.Length];

            var unbiased = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet);
            Assert.AreEqual(0, unbiased.Pops);

            var biased = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 0, projectileContactRadius: 0f,
                workingSet: workingSet, radiusBias: 0.1f);
            Assert.AreEqual(1, biased.Pops, "the fattened circle covers the wobble band");
        }

        [Test]
        public void Simulate_ExplicitDefaultSeed_MatchesOmittedSeed()
        {
            // Phase C0 (@ref plan_shot_solver_accuracy Phase C §5): ShotFlightSeed folds the four
            // dropped starting* test params into one struct — default(ShotFlightSeed) must reproduce
            // the pre-fold "no starting state" behavior exactly, on the suite's own streak-climb board.
            var board = new[]
            {
                ShotBoardBuilder.Green(new Vector2(0f, 1f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 2f), 0.1f, "Red", 1, 1),
                ShotBoardBuilder.Green(new Vector2(0f, 3f), 0.1f, "Red", 1, 1),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var omitted = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet);

            var explicitDefault = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, seed: default(ShotFlightSeed));

            Assert.AreEqual(omitted.RawScore, explicitDefault.RawScore);
            Assert.AreEqual(omitted.Pops, explicitDefault.Pops);
            Assert.AreEqual(omitted.BoardCleared, explicitDefault.BoardCleared);
            Assert.AreEqual(1 + 2 + 3, explicitDefault.RawScore, "unchanged from pre-fold: streak still climbs 1, 2, 3");
        }

        [Test]
        public void CopyIntoWorkingSet_StaticSnapshotWithoutBalanceProfile_LeavesActorNull()
        {
            // A static contact (Phase A's Deflector/Gatekeeper/Absorber shape) has no BalanceProfile,
            // so ShotBoardDynamics builds no dynamic stub for it — the sim must still resolve the
            // contact at the snapshot's own fixed Position, with no NRE anywhere along the null-Actor
            // path (CopyIntoWorkingSet, CurrentBalloonCenter, ResolveBalloonContact).
            var board = new[]
            {
                ShotBoardBuilder.Static(Vector2Int.zero, new Vector2(0f, 2f), 0.2f, ShotContactKind.Poppable),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(1, 1));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0f);

            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, board, Array.Empty<ShotDynamicActorSnapshot>(),
                Array.Empty<ShotStaticActorSnapshot>());

            var result = ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, dynamics: dynamics);

            Assert.AreEqual(1, result.Pops, "the static contact still pops at its literal snapshot position");
            Assert.IsTrue(result.BoardCleared);
            Assert.IsNull(workingSet[0].Actor, "no BalanceProfile means no dynamic stub backs this entry");
        }

        [Test]
        public void CopyIntoWorkingSet_ColorSnapshotWithBalanceProfile_Unaffected()
        {
            // The common (today, only) case a live gather produces: a colour target WITH a
            // BalanceProfile still gets a live dynamic stub — the null-gating added for statics must
            // not regress it.
            var gridConfig = Substitute.For<ISlotGridConfig>();
            gridConfig.SlotsSize.Returns(new Vector2Int(1, 1));
            gridConfig.SlotSeparation.Returns(new Vector2(1f, 1f));
            gridConfig.SlotsOffset.Returns(Vector2.zero);
            var balloonsConfig = Substitute.For<IBalloonsConfiguration>();
            balloonsConfig.FlightRebalanceInterval.Returns(0f);

            var board = new[]
            {
                ShotBoardBuilder.Green(
                    new Vector2(0f, 2f), 0.2f, "Red", 1, 1,
                    slotIndex: Vector2Int.zero, balancePriority: 0, maxBalanceSteps: 0, moveSpeed: 1f,
                    directBalanceMotion: false, nudgeOverrides: null),
            };
            var workingSet = new ShotBalloonState[board.Length];

            var dynamics = new ShotBoardDynamics(
                gridConfig, balloonsConfig, board, Array.Empty<ShotDynamicActorSnapshot>(),
                Array.Empty<ShotStaticActorSnapshot>());

            // maxEvents: 0 stops the flight before any event resolves — CopyIntoWorkingSet already ran
            // (once, up front), which is the only thing this test needs to have happened.
            ShotSimulator.Simulate(
                board, WideOpenWalls, Vector2.zero, Vector2.up, startingShields: 1, projectileContactRadius: 0f,
                workingSet: workingSet, dynamics: dynamics, maxEvents: 0);

            Assert.IsNotNull(workingSet[0].Actor, "a BalanceProfile-carrying snapshot still gets a live stub");
        }
    }
}
