using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Shared
{
    /// <summary>
    /// Tests the bounce impact-magnitude formula used in ProjectileShieldView.OnBounce:
    ///   impactMagnitude = (1 - dot(oldDir, newDir)) * 0.5
    /// Range: 0 (same direction) → 1 (180° reversal).
    /// This is a math formula that could reasonably break (sign error, wrong scale).
    /// </summary>
    [TestFixture]
    public class ImpactMagnitudeTests
    {
        /// <summary>
        /// Computes impact magnitude using the same formula as ProjectileShieldView.OnBounce.
        /// Kept in sync manually — if the formula changes there, update here.
        /// </summary>
        private static float ComputeImpactMagnitude(Vector2 oldDirection, Vector2 newDirection)
        {
            var oldDir = oldDirection.normalized;
            var newDir = newDirection.normalized;
            return (1f - Vector2.Dot(oldDir, newDir)) * 0.5f;
        }

        [Test]
        public void SameDirection_ReturnsZero()
        {
            var magnitude = ComputeImpactMagnitude(Vector2.up, Vector2.up);

            Assert.AreEqual(0f, magnitude, 0.001f,
                "No direction change should produce zero impact");
        }

        [Test]
        public void OppositeDirection_ReturnsOne()
        {
            var magnitude = ComputeImpactMagnitude(Vector2.up, Vector2.down);

            Assert.AreEqual(1f, magnitude, 0.001f,
                "180° reversal should produce maximum impact of 1.0");
        }

        [Test]
        public void PerpendicularDirections_ReturnsHalf()
        {
            var magnitude = ComputeImpactMagnitude(Vector2.up, Vector2.right);

            Assert.AreEqual(0.5f, magnitude, 0.001f,
                "90° turn should produce 0.5 impact");
        }

        [Test]
        public void SmallDeflection_ReturnsSmallValue()
        {
            // ~15° deflection
            var oldDir = Vector2.up;
            var newDir = new Vector2(Mathf.Sin(15f * Mathf.Deg2Rad), Mathf.Cos(15f * Mathf.Deg2Rad));

            var magnitude = ComputeImpactMagnitude(oldDir, newDir);

            Assert.Less(magnitude, 0.1f,
                "Small deflection should produce a small impact magnitude");
            Assert.Greater(magnitude, 0f,
                "Non-zero deflection should produce non-zero impact");
        }

        [Test]
        public void FormulaIsSymmetric()
        {
            var a = new Vector2(0.7f, 0.3f);
            var b = new Vector2(-0.2f, 0.9f);

            var magAB = ComputeImpactMagnitude(a, b);
            var magBA = ComputeImpactMagnitude(b, a);

            Assert.AreEqual(magAB, magBA, 0.001f,
                "Impact magnitude should be symmetric — dot product is commutative");
        }

        [Test]
        public void ResultAlwaysInZeroToOneRange()
        {
            // Sweep angles from 0° to 180°
            for (var degrees = 0; degrees <= 180; degrees += 5)
            {
                var rad = degrees * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Sin(rad), Mathf.Cos(rad));

                var magnitude = ComputeImpactMagnitude(Vector2.up, dir);

                Assert.GreaterOrEqual(magnitude, -0.001f,
                    $"Impact at {degrees}° should not be negative");
                Assert.LessOrEqual(magnitude, 1.001f,
                    $"Impact at {degrees}° should not exceed 1.0");
            }
        }
    }
}
