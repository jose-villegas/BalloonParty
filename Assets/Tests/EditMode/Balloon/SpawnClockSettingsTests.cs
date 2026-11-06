using BalloonParty.Configuration.Level;
using NUnit.Framework;

namespace BalloonParty.Tests.Balloon
{
    [TestFixture]
    public class SpawnClockSettingsTests
    {
        // 3s at level 1, halving per level, never under 0.5s.
        private readonly SpawnClockSettings _settings = new(3f, 0.5f, 0.5f, 4f);

        [Test]
        public void IntervalForLevel_LevelOne_IsTheFirstInterval()
        {
            Assert.AreEqual(3f, _settings.IntervalForLevel(1), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_LaterLevels_DecayPerLevel()
        {
            Assert.AreEqual(1.5f, _settings.IntervalForLevel(2), 0.0001f);
            Assert.AreEqual(0.75f, _settings.IntervalForLevel(3), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_DeepLevel_FloorsAtTheMinimum()
        {
            Assert.AreEqual(0.5f, _settings.IntervalForLevel(4), 0.0001f);
            Assert.AreEqual(0.5f, _settings.IntervalForLevel(500), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_ZeroOrNegativeLevel_RunsAtLevelOnePace()
        {
            Assert.AreEqual(3f, _settings.IntervalForLevel(0), 0.0001f);
            Assert.AreEqual(3f, _settings.IntervalForLevel(-3), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_NoDecay_StaysConstant()
        {
            var flat = new SpawnClockSettings(3f, 1f, 1.25f, 4f);

            Assert.AreEqual(3f, flat.IntervalForLevel(40), 0.0001f);
        }
    }
}
