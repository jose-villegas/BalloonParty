using BalloonParty.Thrower;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Thrower
{
    [TestFixture]
    public class ThrowerControllerAimQuantizationTests
    {
        [Test]
        public void QuantizeAimDirection_ZeroStep_ReturnsDirectionUnchanged()
        {
            var direction = new Vector3(0.6f, 0.8f, 0f);

            var result = ThrowerController.QuantizeAimDirection(direction, 0f);

            Assert.AreEqual(direction, result);
        }

        [Test]
        public void QuantizeAimDirection_NegativeStep_ReturnsDirectionUnchanged()
        {
            var direction = new Vector3(0.6f, 0.8f, 0f);

            var result = ThrowerController.QuantizeAimDirection(direction, -15f);

            Assert.AreEqual(direction, result);
        }

        [Test]
        public void QuantizeAimDirection_DegenerateDirection_ReturnsDirectionUnchanged()
        {
            var direction = Vector3.zero;

            var result = ThrowerController.QuantizeAimDirection(direction, 15f);

            Assert.AreEqual(direction, result);
        }

        [Test]
        public void QuantizeAimDirection_SnapsToNearestStep_NotFloor()
        {
            // 46 degrees is closer to 45 than to 30 — a floor would wrongly land on 30.
            var angle = 46f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.QuantizeAimDirection(direction, 15f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(45f, resultAngle, 0.001f);
        }

        [Test]
        public void QuantizeAimDirection_NearestStepBelow_SnapsDown()
        {
            // 44 degrees is closer to 45 than to 30, but on the other side — confirms nearest works both
            // ways, not just rounding up.
            var angle = 44f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.QuantizeAimDirection(direction, 15f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(45f, resultAngle, 0.001f);
        }

        [Test]
        public void QuantizeAimDirection_WrapsAcrossZeroThreeSixty()
        {
            // -2 degrees (i.e. 358) with a 15-degree step should snap to 0, not -15 or 15 — the wrap
            // around the 0/360 seam must not throw off the nearest-step choice.
            var angle = -2f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.QuantizeAimDirection(direction, 15f);

            var resultAngle = Mathf.Atan2(result.y, result.x) * Mathf.Rad2Deg;
            Assert.AreEqual(0f, resultAngle, 0.001f);
        }

        [Test]
        public void QuantizeAimDirection_ResultIsUnitLength()
        {
            var angle = 17f * Mathf.Deg2Rad;
            var direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

            var result = ThrowerController.QuantizeAimDirection(direction, 10f);

            Assert.AreEqual(1f, result.magnitude, 0.0001f);
        }
    }
}
