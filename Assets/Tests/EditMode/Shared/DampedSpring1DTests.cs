using BalloonParty.Shared.Math;
using NUnit.Framework;

namespace BalloonParty.Tests.Shared
{
    [TestFixture]
    public class DampedSpring1DTests
    {
        [Test]
        public void Step_ConvergesTowardTarget_PositionApproachesTarget()
        {
            var spring = new DampedSpring1D(0f);
            const float target = 5f;

            // Many steps at a moderate frequency and heavy damping — should converge.
            for (var i = 0; i < 300; i++)
            {
                spring.Step(target, frequency: 4f, damping: 1f, dt: 0.016f);
            }

            Assert.AreEqual(target, spring.Position, 0.05f,
                "Spring should converge to target within tolerance after sufficient steps");
        }

        [Test]
        public void Step_ZeroDt_PositionUnchanged()
        {
            var spring = new DampedSpring1D(3f);
            spring.Step(10f, frequency: 4f, damping: 1f, dt: 0f);

            Assert.AreEqual(3f, spring.Position, 0.001f,
                "Zero dt should produce no movement");
        }

        [Test]
        public void AddImpulse_IncreasesVelocity()
        {
            var spring = new DampedSpring1D(0f);
            Assert.AreEqual(0f, spring.Velocity, 0.001f);

            spring.AddImpulse(7.5f);

            Assert.AreEqual(7.5f, spring.Velocity, 0.001f,
                "AddImpulse should add directly to Velocity");
        }

        [Test]
        public void AddImpulse_AccumulatesMultipleImpulses()
        {
            var spring = new DampedSpring1D(0f);
            spring.AddImpulse(2f);
            spring.AddImpulse(3f);

            Assert.AreEqual(5f, spring.Velocity, 0.001f,
                "Multiple impulses should accumulate additively");
        }

        [Test]
        public void Reset_ZerosVelocityAndSetsPosition()
        {
            var spring = new DampedSpring1D(0f);
            spring.AddImpulse(10f);
            spring.Step(5f, frequency: 4f, damping: 0.5f, dt: 0.1f);

            spring.Reset(42f);

            Assert.AreEqual(42f, spring.Position, 0.001f,
                "Reset should set Position to the given value");
            Assert.AreEqual(0f, spring.Velocity, 0.001f,
                "Reset should zero Velocity — prevents phantom squash after pool recycle");
        }

        [Test]
        public void Step_WithImpulse_OvershotThenSettles()
        {
            // An impulse away from target should cause overshoot before settling.
            var spring = new DampedSpring1D(0f);
            spring.AddImpulse(20f); // Large kick away from rest

            float maxPosition = 0f;

            for (var i = 0; i < 500; i++)
            {
                spring.Step(0f, frequency: 4f, damping: 0.6f, dt: 0.016f);

                if (spring.Position > maxPosition)
                {
                    maxPosition = spring.Position;
                }
            }

            Assert.Greater(maxPosition, 0.25f,
                "Impulse should cause overshoot beyond rest position");
            Assert.AreEqual(0f, spring.Position, 0.1f,
                "Spring should settle back near rest after many damped steps");
        }
    }
}
