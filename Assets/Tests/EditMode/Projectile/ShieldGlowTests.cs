using BalloonParty.Projectile.View;
using NUnit.Framework;

namespace BalloonParty.Tests.Projectile
{
    /// <summary>
    ///     <see cref="ShieldGlow.For" /> maps a shield count to an HDR multiplier: palette colour at one
    ///     shield, then a capped step per extra shield, with bad tuning values clamped rather than dimming.
    /// </summary>
    [TestFixture]
    public class ShieldGlowTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(-3)]
        public void For_ZeroOrOneShield_IsOne(int shields)
        {
            Assert.AreEqual(1f, ShieldGlow.For(shields, 0.5f, 3f), 1e-5f);
        }

        [Test]
        public void For_ExtraShields_AddsPerShieldStep()
        {
            Assert.AreEqual(1.5f, ShieldGlow.For(2, 0.5f, 3f), 1e-5f);
            Assert.AreEqual(2f, ShieldGlow.For(3, 0.5f, 3f), 1e-5f);
        }

        [Test]
        public void For_ManyShields_ClampsAtMax()
        {
            Assert.AreEqual(3f, ShieldGlow.For(50, 0.5f, 3f), 1e-5f);
        }

        [Test]
        public void For_NegativePerShield_NeverDimsBelowOne()
        {
            Assert.AreEqual(1f, ShieldGlow.For(5, -1f, 3f), 1e-5f);
        }

        [Test]
        public void For_MaxBelowOne_ClampsToOne()
        {
            Assert.AreEqual(1f, ShieldGlow.For(5, 0.5f, 0.2f), 1e-5f);
        }
    }
}
