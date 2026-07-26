using BalloonParty.Shared.SceneLight;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class TimeOfDayNightTests
    {
        // Night is the small arc 270 deg-315 deg (the sun sweeps 315 down to 270).
        [TestCase(315f, true)]    // start of the arc
        [TestCase(295f, true)]    // middle
        [TestCase(270f, true)]    // end of the arc
        [TestCase(316f, false)]   // just above the arc
        [TestCase(269f, false)]   // just below the arc
        [TestCase(135f, false)]   // far from the arc
        [TestCase(0f, false)]
        [TestCase(200f, false)]
        public void IsNightAngle_MapsAngleToTheNightWindow(float angleDegrees, bool expectedNight)
        {
            Assert.AreEqual(expectedNight, TimeOfDayService.IsNightAngle(angleDegrees));
        }
    }
}
