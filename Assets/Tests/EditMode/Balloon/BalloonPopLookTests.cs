using BalloonParty.Configuration.Balloons;
using BalloonParty.Shared.Extensions;
using NUnit.Framework;
using UnityEngine;

namespace BalloonParty.Tests.Balloon
{
    /// <summary>
    ///     How a pop is shown: an effect authored to fly up turned onto the shot's heading, and the pulse a
    ///     balloon swells and shrinks with when its entry asks for one.
    /// </summary>
    [TestFixture]
    public class BalloonPopLookTests
    {
        [TestCase(0f, 1f, 0f)]
        [TestCase(1f, 0f, -90f)]
        [TestCase(-1f, 0f, 90f)]
        [TestCase(0f, -1f, 180f)]
        public void AnAlongHitEffect_TurnsItsUpOntoTheShot(float x, float y, float expectedZ)
        {
            var rotation = new Vector3(x, y, 0f).UpAlongXY();

            var up = rotation * Vector3.up;
            Assert.AreEqual(x, up.x, 1e-4f);
            Assert.AreEqual(y, up.y, 1e-4f);
            Assert.AreEqual(0f, Quaternion.Angle(rotation, Quaternion.Euler(0f, 0f, expectedZ)), 1e-3f);
        }

        // A board clear pops with no shot behind it: the effect stays as authored.
        [Test]
        public void APopWithNoHeading_LeavesTheEffectUpright()
        {
            Assert.AreEqual(Quaternion.identity, Vector3.zero.UpAlongXY());
            Assert.AreEqual(Quaternion.identity, new Vector3(0f, 0f, 1f).UpAlongXY());
        }

        [TestCase(0f, false)]
        [TestCase(1f, false)]
        [TestCase(1.25f, true)]
        public void APulse_PlaysOnlyWhenItSwellsPastTheBalloonsSize(float peak, bool enabled)
        {
            Assert.AreEqual(enabled, new PopPulse(peak, 0.08f, 0.14f).Enabled);
        }

        // Every other entry in the catalogue leaves the field out, so a fresh one must not pulse.
        [Test]
        public void ANewEntry_PopsWithoutAPulse()
        {
            Assert.IsFalse(new BalloonPrefabEntry().PopPulse.Enabled);
        }
    }
}
