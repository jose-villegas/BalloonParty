using BalloonParty.Editor.ShotSolver;
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
    }
}
