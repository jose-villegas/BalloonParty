using NUnit.Framework;
using UnityEngine;
using BalloonParty.Solver;

namespace BalloonParty.Tests.ShotSolver
{
    /// <summary>Pins <see cref="ShotBoardGather.ResolveSweepSampleCount" />/<see cref="ShotBoardGather.ResolveSweepAngle" />
    /// — the shared sample-grid derivation both <c>FireBestShotCheat</c> and <c>ShotSolverWindow</c>
    /// sweep against, so a quantized aim (<c>IProjectileFlightConfig.AimAngleStepDegrees</c>) only
    /// ever samples angles the player could actually reach.</summary>
    [TestFixture]
    public class ShotBoardGatherSweepTests
    {
        private const float ArcMinDegrees = 10f;
        private const float ArcMaxDegrees = 170f;
        private const int ContinuousSampleCount = 1024;

        [Test]
        public void ResolveSweepSampleCount_ZeroStep_ReturnsContinuousCountUnchanged()
        {
            var count = ShotBoardGather.ResolveSweepSampleCount(
                ArcMinDegrees, ArcMaxDegrees, 0f, ContinuousSampleCount);

            Assert.AreEqual(ContinuousSampleCount, count);
        }

        [Test]
        public void ResolveSweepAngle_ZeroStep_MatchesOldContinuousLerp()
        {
            // Same formula the sweep used before quantization existed — pins that continuous aim is
            // byte-for-byte unchanged.
            for (var i = 0; i < ContinuousSampleCount; i += 97)
            {
                var expected = Mathf.Lerp(ArcMinDegrees, ArcMaxDegrees, i / (float)(ContinuousSampleCount - 1));
                var actual = ShotBoardGather.ResolveSweepAngle(
                    i, ArcMinDegrees, ArcMaxDegrees, 0f, ContinuousSampleCount);

                Assert.AreEqual(expected, actual, 0.0001f);
            }
        }

        [Test]
        public void ResolveSweepSampleCount_NegativeStep_TreatedAsContinuous()
        {
            var count = ShotBoardGather.ResolveSweepSampleCount(
                ArcMinDegrees, ArcMaxDegrees, -5f, ContinuousSampleCount);

            Assert.AreEqual(ContinuousSampleCount, count);
        }

        [Test]
        public void ResolveSweepSampleCount_StepDividesArcEvenly_CountsEveryGridLineInclusive()
        {
            // 10..170 by 5 lands exactly on both ends: 33 multiples of 5.
            var count = ShotBoardGather.ResolveSweepSampleCount(ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount);

            Assert.AreEqual(33, count);
        }

        [Test]
        public void ResolveSweepAngle_StepDividesArcEvenly_FirstAndLastAnglesAreArcBounds()
        {
            var count = ShotBoardGather.ResolveSweepSampleCount(ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount);

            Assert.AreEqual(ArcMinDegrees, ShotBoardGather.ResolveSweepAngle(0, ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount), 0.0001f);
            Assert.AreEqual(ArcMaxDegrees, ShotBoardGather.ResolveSweepAngle(count - 1, ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount), 0.0001f);
        }

        [Test]
        public void ResolveSweepAngle_StepDividesArcEvenly_EveryAngleIsOnGridAndWithinArc()
        {
            const float step = 5f;
            var count = ShotBoardGather.ResolveSweepSampleCount(ArcMinDegrees, ArcMaxDegrees, step, ContinuousSampleCount);

            for (var i = 0; i < count; i++)
            {
                var angle = ShotBoardGather.ResolveSweepAngle(i, ArcMinDegrees, ArcMaxDegrees, step, ContinuousSampleCount);

                Assert.GreaterOrEqual(angle, ArcMinDegrees);
                Assert.LessOrEqual(angle, ArcMaxDegrees);
                var stepsFromZero = angle / step;
                Assert.AreEqual(Mathf.Round(stepsFromZero), stepsFromZero, 0.0001f,
                    $"angle {angle} is not a multiple of the {step}° step");
            }
        }

        [Test]
        public void ResolveSweepSampleCount_StepDoesNotDivideArcEvenly_CountsOnlyTheReachableAngles()
        {
            // Reachable multiples of 5 in [12, 27]: 15, 20, 25 — three, not five.
            var count = ShotBoardGather.ResolveSweepSampleCount(12f, 27f, 5f, ContinuousSampleCount);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void ResolveSweepAngle_StepDoesNotDivideArcEvenly_FirstSampleIsNotArcMinItself()
        {
            // The grid is anchored at absolute multiples of the step, not at the arc's own min — 12
            // is not a multiple of 5, so the first reachable angle inside [12, 27] is 15.
            var first = ShotBoardGather.ResolveSweepAngle(0, 12f, 27f, 5f, ContinuousSampleCount);

            Assert.AreEqual(15f, first, 0.0001f);
        }

        [Test]
        public void ResolveSweepSampleCount_StepWiderThanArc_StillYieldsAtLeastOneSample()
        {
            // No multiple of 200 falls inside [10, 12] — the guard must still return >= 1.
            var count = ShotBoardGather.ResolveSweepSampleCount(10f, 12f, 200f, ContinuousSampleCount);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void ResolveSweepAngle_StepWiderThanArc_PicksTheNearestReachableGridLine()
        {
            // Candidates are 0 (below the arc, distance 10) and 200 (above the arc, distance 188) —
            // 0 is nearer.
            var angle = ShotBoardGather.ResolveSweepAngle(0, 10f, 12f, 200f, ContinuousSampleCount);

            Assert.AreEqual(0f, angle, 0.0001f);
        }

        [Test]
        public void ResolveSweepAngle_StepWiderThanArc_PicksTheOtherNeighbourWhenItIsCloser()
        {
            // Candidates are 0 (distance 100) and 200 (distance 30) — 200 is nearer.
            var angle = ShotBoardGather.ResolveSweepAngle(0, 100f, 170f, 200f, ContinuousSampleCount);

            Assert.AreEqual(200f, angle, 0.0001f);
        }
    }
}
