using BalloonParty.Shared;
using BalloonParty.Thrower;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Thrower
{
    [TestFixture]
    public class ThrowerControllerAimQuantizationTests
    {
        // Wide enough that none of the plain quantization cases below ever clamp — isolates their
        // assertions to the snapping behaviour alone, the same way they worked before the range
        // became mandatory.
        private const float WideMinDegrees = -180f;
        private const float WideMaxDegrees = 180f;

        [Test]
        public void ClampAndQuantizeAimDirection_ZeroStep_ReturnsDirectionUnchangedWithinRange()
        {
            var direction = new Vector3(0.6f, 0.8f, 0f);
            var expectedAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 0f, WideMinDegrees, WideMaxDegrees);

            // Continuous aim still round-trips through Atan2/Cos/Sin (see ClampAndQuantizeAimDirection's
            // own remarks — there is no short-circuit for "already reachable"), so the returned direction
            // is only equal to within float precision, not bit-for-bit — comparing the recovered angle
            // against a tolerance is what every other angle-producing test in this file already does.
            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(expectedAngle, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_NegativeStep_ReturnsDirectionUnchangedWithinRange()
        {
            var direction = new Vector3(0.6f, 0.8f, 0f);
            var expectedAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, -15f, WideMinDegrees, WideMaxDegrees);

            // Same Atan2/Cos/Sin round-trip as the zero-step case above — a negative step is also treated
            // as continuous, so this isn't bit-exact either.
            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(expectedAngle, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_DegenerateDirection_ReturnsDirectionUnchanged()
        {
            var direction = Vector3.zero;

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 15f, 5f, 175f);

            Assert.AreEqual(direction, result);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_SnapsToNearestStep_NotFloor()
        {
            // 46 degrees is closer to 45 than to 30 — a floor would wrongly land on 30.
            var angle = 46f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 15f, WideMinDegrees, WideMaxDegrees);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(45f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_NearestStepBelow_SnapsDown()
        {
            // 44 degrees is closer to 45 than to 30, but on the other side — confirms nearest works both
            // ways, not just rounding up.
            var angle = 44f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 15f, WideMinDegrees, WideMaxDegrees);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(45f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_WrapsAcrossZeroThreeSixty()
        {
            // -2 degrees (i.e. 358) with a 15-degree step should snap to 0, not -15 or 15 — the wrap
            // around the 0/360 seam must not throw off the nearest-step choice. A wide-open range keeps
            // this isolated from the clamp.
            var angle = -2f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 15f, WideMinDegrees, WideMaxDegrees);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(0f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_ResultIsUnitLength()
        {
            var angle = 17f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 10f, WideMinDegrees, WideMaxDegrees);

            Assert.AreEqual(1f, result.magnitude, 0.0001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_ContinuousAim_RawAngleBelowRange_ClampsToMin()
        {
            // 2 degrees, well below the default 5 degree floor — continuous aim (step 0) is a plain clamp.
            var angle = 2f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 0f, 5f, 175f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(5f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_ContinuousAim_RawAngleAboveRange_ClampsToMax()
        {
            var angle = 178f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 0f, 5f, 175f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(175f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_QuantizedAim_RawAngleOutsideRangeEntirely_LandsOnGridWithinRange()
        {
            // Step of 10 over [5, 175]: reachable multiples are 10, 20, ..., 170. A raw angle of 2
            // degrees is outside the range on the low side AND not itself a multiple of 10 — the result
            // must still be both a reachable multiple and inside [5, 175], never 0 (the nearest bare
            // multiple to the raw angle, but unreachable) and never negative.
            var angle = 2f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 10f, 5f, 175f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(10f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_QuantizedAim_RawAngleAboveRange_LandsOnGridWithinRange()
        {
            var angle = 178f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.ClampAndQuantizeAimDirection(direction, 10f, 5f, 175f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(170f, resultAngle, 0.001f);
        }

        [Test]
        public void ClampAndQuantizeAimDirection_RangeBoundsNotGridMultiples_ResultIsAlwaysOnGridAndWithinRange()
        {
            // The project's actual defaults: neither 5 nor 175 is a multiple of the project's own
            // AimAngleStepDegrees (0.5) in every case, but more sharply here with a 10-degree step —
            // neither bound is a multiple of 10, so this pins that the clamp still produces a value on
            // the grid, not merely inside the range.
            const float step = 10f;
            const float min = 5f;
            const float max = 175f;

            for (var rawDegrees = -30f; rawDegrees <= 210f; rawDegrees += 7f)
            {
                var angle = rawDegrees * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                var result = ThrowerController.ClampAndQuantizeAimDirection(direction, step, min, max);
                var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;

                Assert.GreaterOrEqual(resultAngle, min, $"raw {rawDegrees}° resolved to {resultAngle}°, below the range");
                Assert.LessOrEqual(resultAngle, max, $"raw {rawDegrees}° resolved to {resultAngle}°, above the range");
                var stepsFromZero = resultAngle / step;
                Assert.AreEqual(Mathf.Round(stepsFromZero), stepsFromZero, 0.0001f,
                    $"raw {rawDegrees}° resolved to {resultAngle}°, not a multiple of the {step}° step");
            }
        }

        [Test]
        public void ClampAndQuantizeAimDirection_MatchesAimAngleGridClampToReachableAngle()
        {
            // The whole point of routing through AimAngleGrid is that the thrower's clamp and every
            // sweep built on the same grid can never disagree — pin the delegation directly rather than
            // just its observable effects.
            const float step = 5f;
            const float min = 12f;
            const float max = 27f;

            foreach (var rawDegrees in new[] { -10f, 0f, 12f, 17f, 27f, 40f })
            {
                var angle = rawDegrees * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

                var result = ThrowerController.ClampAndQuantizeAimDirection(direction, step, min, max);
                var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;

                var expected = AimAngleGrid.ClampToReachableAngle(rawDegrees, min, max, step);
                Assert.AreEqual(expected, resultAngle, 0.001f, $"raw {rawDegrees}°");
            }
        }
    }
}
