using BalloonParty.Shared.SceneLight;
using NUnit.Framework;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class TimeOfDayNightTests
    {
        // 12 AM sits at 135 degrees; time decreases the angle 15 deg/hour. Night is 5 PM-4 AM (end exclusive).
        [TestCase(135f, true)]    // 12 AM
        [TestCase(90f, true)]     // 3 AM
        [TestCase(195f, true)]    // 8 PM
        [TestCase(240f, true)]    // 5 PM (start, inclusive)
        [TestCase(75f, false)]    // 4 AM (end, exclusive)
        [TestCase(45f, false)]    // 6 AM
        [TestCase(0f, false)]     // 9 AM
        [TestCase(315f, false)]   // 12 PM
        public void IsNightAngle_MapsAngleToTheNightWindow(float angleDegrees, bool expectedNight)
        {
            Assert.AreEqual(expectedNight, TimeOfDayService.IsNightAngle(angleDegrees));
        }
    }
}
