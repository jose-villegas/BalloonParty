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

        [Test]
        public void IntervalForLevel_PastTheMinimum_KeepsShrinkingAtTheLateRate()
        {
            // 3s halving per level reaches 0.75s at level 3; from there it halves again per level, down to 0.2s.
            var late = new SpawnClockSettings(3f, 0.5f, 0.75f, 4f, lateIntervalDecayPerLevel: 0.5f,
                lateMinInterval: 0.2f);

            Assert.AreEqual(0.75f, late.IntervalForLevel(3), 0.0001f);
            Assert.AreEqual(0.375f, late.IntervalForLevel(4), 0.0001f);
            Assert.AreEqual(0.2f, late.IntervalForLevel(5), 0.0001f);
            Assert.AreEqual(0.2f, late.IntervalForLevel(500), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_KneeBetweenLevels_NeverRisesFromOneLevelToTheNext()
        {
            // The shipped shape: the early decay crosses 0.9s between levels 13 and 14.
            var shipped = new SpawnClockSettings(1.7f, 0.95f, 0.9f, 2.5f, lateIntervalDecayPerLevel: 0.98f,
                lateMinInterval: 0.55f);

            for (var level = 1; level < 80; level++)
            {
                Assert.LessOrEqual(shipped.IntervalForLevel(level + 1), shipped.IntervalForLevel(level),
                    $"level {level + 1} is slower than level {level}");
            }

            Assert.Less(shipped.IntervalForLevel(36), shipped.IntervalForLevel(20));
            Assert.Less(shipped.IntervalForLevel(20), 0.9f);
        }

        [Test]
        public void IntervalForLevel_FirstIntervalAtTheMinimum_DecaysAtTheLateRateFromLevelOne()
        {
            var settings = new SpawnClockSettings(1f, 0.5f, 1f, 4f, lateIntervalDecayPerLevel: 0.5f,
                lateMinInterval: 0.1f);

            Assert.AreEqual(1f, settings.IntervalForLevel(1), 0.0001f);
            Assert.AreEqual(0.25f, settings.IntervalForLevel(3), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_NoEarlyDecay_NeverReachesTheLateDecay()
        {
            var settings = new SpawnClockSettings(3f, 1f, 1f, 4f, lateIntervalDecayPerLevel: 0.5f,
                lateMinInterval: 0.1f);

            Assert.AreEqual(3f, settings.IntervalForLevel(500), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_NoLateDecay_HoldsAtTheMinimum()
        {
            var settings = new SpawnClockSettings(3f, 0.5f, 0.5f, 4f, lateIntervalDecayPerLevel: 1f,
                lateMinInterval: 0.1f);

            Assert.AreEqual(0.5f, settings.IntervalForLevel(1000), 0.0001f);
        }

        [Test]
        public void IntervalForLevel_LateFloorAboveTheMinimum_HoldsAtTheMinimum()
        {
            var settings = new SpawnClockSettings(3f, 0.5f, 0.5f, 4f, lateIntervalDecayPerLevel: 0.9f,
                lateMinInterval: 2f);

            Assert.AreEqual(0.5f, settings.IntervalForLevel(40), 0.0001f);
        }
    }
}
