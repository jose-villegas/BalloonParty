using BalloonParty.Shared.SceneLight;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class TimeOfDayNightTests
    {
        // Night is the small arc 275 deg-315 deg (the sun sweeps 315 down to 275).
        [TestCase(315f, true)]    // start of the arc
        [TestCase(295f, true)]    // middle
        [TestCase(270f, true)]    // end of the arc
        [TestCase(316f, false)]   // just outside
        [TestCase(274f, false)]   // just outside
        [TestCase(135f, false)]   // far from the arc
        [TestCase(0f, false)]
        [TestCase(200f, false)]
        public void IsNightAngle_MapsAngleToTheNightWindow(float angleDegrees, bool expectedNight)
        {
            Assert.AreEqual(expectedNight, TimeOfDayService.IsNightAngle(angleDegrees));
        }
    }
}
