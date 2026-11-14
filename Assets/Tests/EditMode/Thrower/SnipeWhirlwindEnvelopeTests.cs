using BalloonParty.Thrower;
using NUnit.Framework;

namespace BalloonParty.Tests.Thrower
{
    /// <summary>The whirlwind's strength through its play: it rises, holds and falls, and is gone at both ends.</summary>
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
    }
}
