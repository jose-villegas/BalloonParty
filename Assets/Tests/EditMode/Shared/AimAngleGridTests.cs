using NUnit.Framework;
using UnityEngine;
using BalloonParty.Shared;

namespace BalloonParty.Tests.Shared
{
    /// <summary>Pins <see cref="AimAngleGrid.ResolveSweepSampleCount" />/<see cref="AimAngleGrid.ResolveSweepAngle" />
    /// — the shared sample-grid derivation both <c>FireBestShotCheat</c> and <c>ShotSolverWindow</c>
    /// sweep against, so a quantized aim (<c>IProjectileFlightConfig.AimAngleStepDegrees</c>) only
    /// ever samples angles the player could actually reach.</summary>
    [TestFixture]
    public class AimAngleGridTests
    {
        private const float ArcMinDegrees = 10f;
        private const float ArcMaxDegrees = 170f;
        private const int ContinuousSampleCount = 1024;

        [Test]
        public void ResolveSweepSampleCount_ZeroStep_ReturnsContinuousCountUnchanged()
        {
            var count = AimAngleGrid.ResolveSweepSampleCount(
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
                var actual = AimAngleGrid.ResolveSweepAngle(
                    i, ArcMinDegrees, ArcMaxDegrees, 0f, ContinuousSampleCount);

                Assert.AreEqual(expected, actual, 0.0001f);
            }
        }

        [Test]
        public void ResolveSweepSampleCount_NegativeStep_TreatedAsContinuous()
        {
            var count = AimAngleGrid.ResolveSweepSampleCount(
                ArcMinDegrees, ArcMaxDegrees, -5f, ContinuousSampleCount);

            Assert.AreEqual(ContinuousSampleCount, count);
        }

        [Test]
        public void ResolveSweepSampleCount_StepDividesArcEvenly_CountsEveryGridLineInclusive()
        {
            // 10..170 by 5 lands exactly on both ends: 33 multiples of 5.
            var count = AimAngleGrid.ResolveSweepSampleCount(ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount);

            Assert.AreEqual(33, count);
        }

        [Test]
        public void ResolveSweepAngle_StepDividesArcEvenly_FirstAndLastAnglesAreArcBounds()
        {
            var count = AimAngleGrid.ResolveSweepSampleCount(ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount);

            Assert.AreEqual(ArcMinDegrees, AimAngleGrid.ResolveSweepAngle(0, ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount), 0.0001f);
            Assert.AreEqual(ArcMaxDegrees, AimAngleGrid.ResolveSweepAngle(count - 1, ArcMinDegrees, ArcMaxDegrees, 5f, ContinuousSampleCount), 0.0001f);
        }

        [Test]
        public void ResolveSweepAngle_StepDividesArcEvenly_EveryAngleIsOnGridAndWithinArc()
        {
            const float step = 5f;
            var count = AimAngleGrid.ResolveSweepSampleCount(ArcMinDegrees, ArcMaxDegrees, step, ContinuousSampleCount);

            for (var i = 0; i < count; i++)
            {
                var angle = AimAngleGrid.ResolveSweepAngle(i, ArcMinDegrees, ArcMaxDegrees, step, ContinuousSampleCount);

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
            var count = AimAngleGrid.ResolveSweepSampleCount(12f, 27f, 5f, ContinuousSampleCount);

            Assert.AreEqual(3, count);
        }

        [Test]
        public void ResolveSweepAngle_StepDoesNotDivideArcEvenly_FirstSampleIsNotArcMinItself()
        {
            // The grid is anchored at absolute multiples of the step, not at the arc's own min — 12
            // is not a multiple of 5, so the first reachable angle inside [12, 27] is 15.
            var first = AimAngleGrid.ResolveSweepAngle(0, 12f, 27f, 5f, ContinuousSampleCount);

            Assert.AreEqual(15f, first, 0.0001f);
        }

        [Test]
        public void ResolveSweepSampleCount_StepWiderThanArc_StillYieldsAtLeastOneSample()
        {
            // No multiple of 200 falls inside [10, 12] — the guard must still return >= 1.
            var count = AimAngleGrid.ResolveSweepSampleCount(10f, 12f, 200f, ContinuousSampleCount);

            Assert.AreEqual(1, count);
        }

        [Test]
        public void ResolveSweepAngle_StepWiderThanArc_PicksTheNearestReachableGridLine()
        {
            // Candidates are 0 (below the arc, distance 10) and 200 (above the arc, distance 188) —
            // 0 is nearer.
            var angle = AimAngleGrid.ResolveSweepAngle(0, 10f, 12f, 200f, ContinuousSampleCount);

            Assert.AreEqual(0f, angle, 0.0001f);
        }

        [Test]
        public void ResolveSweepAngle_StepWiderThanArc_PicksTheOtherNeighbourWhenItIsCloser()
        {
            // Candidates are 0 (distance 100) and 200 (distance 30) — 200 is nearer.
            var angle = AimAngleGrid.ResolveSweepAngle(0, 100f, 170f, 200f, ContinuousSampleCount);

            Assert.AreEqual(200f, angle, 0.0001f);
        }

        // ClampToReachableAngle backs ThrowerController.ClampAndQuantizeAimDirection — the thrower's
        // per-frame aim clamp. Pinned here too (not just via the controller) since this is the single
        // source both the thrower and the sweep above share; a regression here would silently desync
        // "what the player can aim at" from "what the sweep considers reachable".

        [Test]
        public void ClampToReachableAngle_ContinuousAim_RawAngleWithinRange_ReturnsRawAngleUnchanged()
        {
            var angle = AimAngleGrid.ClampToReachableAngle(90f, ArcMinDegrees, ArcMaxDegrees, 0f);

            Assert.AreEqual(90f, angle, 0.0001f);
        }

        [Test]
        public void ClampToReachableAngle_ContinuousAim_RawAngleOutsideRange_ClampsToNearestBound()
        {
            Assert.AreEqual(
                ArcMinDegrees, AimAngleGrid.ClampToReachableAngle(-40f, ArcMinDegrees, ArcMaxDegrees, 0f), 0.0001f);
            Assert.AreEqual(
                ArcMaxDegrees, AimAngleGrid.ClampToReachableAngle(400f, ArcMinDegrees, ArcMaxDegrees, 0f), 0.0001f);
        }

        [Test]
        public void ClampToReachableAngle_QuantizedAim_RawAngleWithinRangeOnGrid_ReturnsItUnchanged()
        {
            var angle = AimAngleGrid.ClampToReachableAngle(100f, ArcMinDegrees, ArcMaxDegrees, 5f);

            Assert.AreEqual(100f, angle, 0.0001f);
        }

        [Test]
        public void ClampToReachableAngle_QuantizedAim_RawAngleOutsideRangeEntirely_ClampsToNearestReachableGridLine()
        {
            // -50 is far below the 10..170 arc — must land on the nearest reachable multiple of 5
            // INSIDE the arc (10), not on the nearest bare multiple of 5 to -50 (-50 itself).
            var below = AimAngleGrid.ClampToReachableAngle(-50f, ArcMinDegrees, ArcMaxDegrees, 5f);
            Assert.AreEqual(10f, below, 0.0001f);

            var above = AimAngleGrid.ClampToReachableAngle(500f, ArcMinDegrees, ArcMaxDegrees, 5f);
            Assert.AreEqual(170f, above, 0.0001f);
        }

        [Test]
        public void ClampToReachableAngle_RangeBoundsNotGridMultiples_RawAngleBelowRange_LandsOnFirstReachableLine()
        {
            // Mirrors ResolveSweepAngle_StepDoesNotDivideArcEvenly_FirstSampleIsNotArcMinItself: 12 is
            // not a multiple of 5, so the lowest reachable angle inside [12, 27] is 15, not 12 or 10.
            var angle = AimAngleGrid.ClampToReachableAngle(0f, 12f, 27f, 5f);

            Assert.AreEqual(15f, angle, 0.0001f);
        }

        [Test]
        public void ClampToReachableAngle_RangeBoundsNotGridMultiples_RawAngleInsideRange_SnapsToNearestGridLine()
        {
            // 17 is between the reachable lines 15 and 20 — nearer to 15.
            var angle = AimAngleGrid.ClampToReachableAngle(17f, 12f, 27f, 5f);

            Assert.AreEqual(15f, angle, 0.0001f);
        }

        [Test]
        public void ClampToReachableAngle_StepWiderThanRange_IgnoresRawAngle_MatchesResolveSweepAngleFallback()
        {
            // No multiple of 200 falls inside [10, 12] — ClampToReachableAngle must pick the exact same
            // single reachable angle ResolveSweepAngle's index-0 sample would, regardless of where the
            // raw angle sits, or the thrower and the sweep could disagree about the one angle that's
            // actually reachable.
            var expected = AimAngleGrid.ResolveSweepAngle(0, 10f, 12f, 200f, ContinuousSampleCount);

            Assert.AreEqual(expected, AimAngleGrid.ClampToReachableAngle(-1000f, 10f, 12f, 200f), 0.0001f);
            Assert.AreEqual(expected, AimAngleGrid.ClampToReachableAngle(11f, 10f, 12f, 200f), 0.0001f);
            Assert.AreEqual(expected, AimAngleGrid.ClampToReachableAngle(1000f, 10f, 12f, 200f), 0.0001f);
        }
    }
}
