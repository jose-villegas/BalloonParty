using BalloonParty.Shared.Math;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    /// Tests <see cref="DampedSpring2D"/> — the 2D variant used for shield noise-scroll direction.
    /// Mirrors the <see cref="DampedSpring1DTests"/> structure. The spring formula itself contains
    /// math that could break (omega computation, exponential damping, 2D vector operations).
    /// </summary>
    [TestFixture]
    public class DampedSpring2DTests
    {
        [Test]
        public void Step_ConvergesTowardTarget_PositionApproachesTarget()
        {
            var spring = new DampedSpring2D(Vector2.zero);
            var target = new Vector2(5f, 3f);

            for (var i = 0; i < 300; i++)
            {
                spring.Step(target, frequency: 4f, damping: 1f, dt: 0.016f);
            }

            Assert.AreEqual(target.x, spring.Position.x, 0.05f,
                "Spring X should converge to target within tolerance");
            Assert.AreEqual(target.y, spring.Position.y, 0.05f,
                "Spring Y should converge to target within tolerance");
        }

        [Test]
        public void Step_ZeroDt_PositionUnchanged()
        {
            var initial = new Vector2(3f, -2f);
            var spring = new DampedSpring2D(initial);
            spring.Step(new Vector2(10f, 10f), frequency: 4f, damping: 1f, dt: 0f);

            Assert.AreEqual(initial.x, spring.Position.x, 0.001f,
                "Zero dt should produce no X movement");
            Assert.AreEqual(initial.y, spring.Position.y, 0.001f,
                "Zero dt should produce no Y movement");
        }

        [Test]
        public void AddImpulse_IncreasesVelocity()
        {
            var spring = new DampedSpring2D(Vector2.zero);
            Assert.AreEqual(Vector2.zero, spring.Velocity);

            var impulse = new Vector2(7.5f, -3f);
            spring.AddImpulse(impulse);

            Assert.AreEqual(impulse.x, spring.Velocity.x, 0.001f,
                "AddImpulse should add directly to Velocity X");
            Assert.AreEqual(impulse.y, spring.Velocity.y, 0.001f,
                "AddImpulse should add directly to Velocity Y");
        }

        [Test]
        public void AddImpulse_AccumulatesMultipleImpulses()
        {
            var spring = new DampedSpring2D(Vector2.zero);
            spring.AddImpulse(new Vector2(2f, 1f));
            spring.AddImpulse(new Vector2(3f, -4f));

            Assert.AreEqual(5f, spring.Velocity.x, 0.001f,
                "Multiple impulses should accumulate X additively");
            Assert.AreEqual(-3f, spring.Velocity.y, 0.001f,
                "Multiple impulses should accumulate Y additively");
        }

        [Test]
        public void Reset_ZerosVelocityAndSetsPosition()
        {
            var spring = new DampedSpring2D(Vector2.zero);
            spring.AddImpulse(new Vector2(10f, -5f));
            spring.Step(new Vector2(5f, 5f), frequency: 4f, damping: 0.5f, dt: 0.1f);

            var resetPos = new Vector2(42f, -7f);
            spring.Reset(resetPos);

            Assert.AreEqual(resetPos.x, spring.Position.x, 0.001f,
                "Reset should set Position X to the given value");
            Assert.AreEqual(resetPos.y, spring.Position.y, 0.001f,
                "Reset should set Position Y to the given value");
            Assert.AreEqual(0f, spring.Velocity.x, 0.001f,
                "Reset should zero Velocity X — prevents phantom noise after pool recycle");
            Assert.AreEqual(0f, spring.Velocity.y, 0.001f,
                "Reset should zero Velocity Y");
        }

        [Test]
        public void Step_WithImpulse_OvershotThenSettles()
        {
            var spring = new DampedSpring2D(Vector2.zero);
            spring.AddImpulse(new Vector2(20f, 0f));

            var maxX = 0f;

            for (var i = 0; i < 500; i++)
            {
                spring.Step(Vector2.zero, frequency: 4f, damping: 0.6f, dt: 0.016f);

                if (spring.Position.x > maxX)
                {
                    maxX = spring.Position.x;
                }
            }

            Assert.Greater(maxX, 0.25f,
                "Impulse should cause overshoot beyond rest position");
            Assert.AreEqual(0f, spring.Position.x, 0.1f,
                "Spring should settle back near rest after many damped steps");
        }

        [Test]
        public void Step_IndependentAxes_ConvergeIndependently()
        {
            // Target only in Y — X should stay near zero, Y should converge.
            var spring = new DampedSpring2D(Vector2.zero);
            var target = new Vector2(0f, 10f);

            for (var i = 0; i < 300; i++)
            {
                spring.Step(target, frequency: 4f, damping: 1f, dt: 0.016f);
            }

            Assert.AreEqual(0f, spring.Position.x, 0.05f,
                "X should remain near zero when target X is zero");
            Assert.AreEqual(10f, spring.Position.y, 0.05f,
                "Y should converge to target");
        }
    }
}
