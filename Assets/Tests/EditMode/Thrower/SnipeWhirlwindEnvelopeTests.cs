using BalloonParty.Thrower;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Thrower
{
    /// <summary>
    ///     The whirlwind's strength through its play (it rises, holds and falls, and is gone at both ends), and the
    ///     sky light's fall straight down onto the cannon.
    /// </summary>
    [TestFixture]
    public class SnipeWhirlwindEnvelopeTests
    {
        [TestCase(0f, 0f)]
        [TestCase(0.1f, 0.5f)]
        [TestCase(0.2f, 1f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.775f, 0.5f)]
        [TestCase(1f, 0f)]
        public void Envelope_RisesHoldsAndFalls(float t, float expected)
        {
            Assert.AreEqual(expected, SnipeWhirlwindView.Envelope(t, 0.2f, 0.45f), 1e-4f);
        }

        [Test]
        public void Envelope_NoFades_IsFullThroughout()
        {
            Assert.AreEqual(1f, SnipeWhirlwindView.Envelope(0.01f, 0f, 0f));
            Assert.AreEqual(1f, SnipeWhirlwindView.Envelope(0.99f, 0f, 0f));
        }

        [Test]
        public void SkyLight_StartsStraightAboveTheLanding()
        {
            var landing = new Vector3(0.5f, -4.6f, 0f);

            AssertNear(new Vector3(0.5f, 5.4f, 0f), SnipeWhirlwindView.SkyLightPosition(landing, 10f, 0f));
        }

        [Test]
        public void SkyLight_HalfwayDown_IsHalfTheHeightAbove()
        {
            var landing = new Vector3(0f, -4.6f, 0f);

            AssertNear(new Vector3(0f, 0.4f, 0f), SnipeWhirlwindView.SkyLightPosition(landing, 10f, 0.5f));
        }

        [TestCase(1f)]
        [TestCase(1.2f)]
        public void SkyLight_Fallen_IsOnTheLanding(float fall)
        {
            var landing = new Vector3(0f, -4.6f, 0f);

            AssertNear(landing, SnipeWhirlwindView.SkyLightPosition(landing, 10f, fall));
        }

        [TestCase(1.5f, 1f)]
        [TestCase(2f, 1.4f)]
        [TestCase(0.12f, 0.4f)]
        public void SkyLightSeconds_FillsTheReloadLeftAfterTheWhirlwind_WithinItsBounds(float reload, float expected)
        {
            Assert.AreEqual(expected, SnipeWhirlwindView.SkyLightSeconds(reload, 0.5f, 0.4f, 1.4f), 1e-5f);
        }

        [Test]
        public void SkyLightSeconds_NoMax_SkipsTheLight()
        {
            Assert.AreEqual(0f, SnipeWhirlwindView.SkyLightSeconds(2f, 0.5f, 0.4f, 0f));
        }

        private static void AssertNear(Vector3 expected, Vector3 actual)
        {
            Assert.Less(Vector3.Distance(expected, actual), 1e-4f, $"expected {expected}, got {actual}");
        }
    }
}
