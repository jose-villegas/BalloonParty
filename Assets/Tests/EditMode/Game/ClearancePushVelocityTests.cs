using BalloonParty.Game.Score.Behaviours;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class ClearancePushVelocityTests
    {
        private const float MaxSpeed = 14f;
        private const float Frame = 1f / 60f;

        [Test]
        public void SmoothClearanceVelocity_FromRest_FirstFrameIsAFractionOfTheCap()
        {
            var velocity = ShapeFormationTicker.SmoothClearanceVelocity(
                Vector3.zero, new Vector3(5f, 0f, 0f), MaxSpeed, Frame);

            Assert.Greater(velocity.x, 0f, "it starts moving out of the circle");
            Assert.Less(velocity.x, MaxSpeed * 0.5f, "it eases in rather than jumping to the cap");
            Assert.AreEqual(0f, velocity.y, 1e-6f);
        }

        [Test]
        public void SmoothClearanceVelocity_HeldOverlap_ConvergesToTheCapAndNeverPassesIt()
        {
            var velocity = Vector3.zero;
            for (var i = 0; i < 600; i++)
            {
                velocity = ShapeFormationTicker.SmoothClearanceVelocity(
                    velocity, new Vector3(0f, 10f, 0f), MaxSpeed, Frame);
                Assert.LessOrEqual(velocity.magnitude, MaxSpeed + 1e-4f);
            }

            Assert.AreEqual(MaxSpeed, velocity.magnitude, 1e-3f);
        }

        [Test]
        public void SmoothClearanceVelocity_OverlapCleared_CoastsDownToRestWithoutReversing()
        {
            var velocity = new Vector3(MaxSpeed, 0f, 0f);
            var previous = velocity.x;
            for (var i = 0; i < 120; i++)
            {
                velocity = ShapeFormationTicker.SmoothClearanceVelocity(velocity, Vector3.zero, MaxSpeed, Frame);
                Assert.GreaterOrEqual(velocity.x, 0f, "it slows, it doesn't bounce back");
                Assert.LessOrEqual(velocity.x, previous);
                previous = velocity.x;
            }

            Assert.Less(velocity.magnitude, 0.01f);
        }

        [Test]
        public void SmoothClearanceVelocity_OverlapFlips_NoSingleFrameReversesTheVelocity()
        {
            var velocity = new Vector3(MaxSpeed, 0f, 0f);

            velocity = ShapeFormationTicker.SmoothClearanceVelocity(
                velocity, new Vector3(-5f, 0f, 0f), MaxSpeed, Frame);

            Assert.Greater(velocity.x, 0f, "a ball passing through flips the overlap, but the shape turns over frames");
        }

        [Test]
        public void SmoothClearanceVelocity_TinyOverlap_SlowerThanTheCap()
        {
            var velocity = Vector3.zero;
            for (var i = 0; i < 600; i++)
            {
                velocity = ShapeFormationTicker.SmoothClearanceVelocity(
                    velocity, new Vector3(0.1f, 0f, 0f), MaxSpeed, Frame);
            }

            Assert.Less(velocity.magnitude, MaxSpeed, "a graze nudges the shape, it doesn't fling it");
            Assert.Greater(velocity.magnitude, 0f);
        }

        [Test]
        public void SmoothClearanceVelocity_ZeroDt_KeepsTheVelocity()
        {
            var velocity = new Vector3(3f, -2f, 0f);

            var result = ShapeFormationTicker.SmoothClearanceVelocity(
                velocity, new Vector3(5f, 5f, 0f), MaxSpeed, 0f);

            Assert.AreEqual(velocity, result);
        }
    }
}
