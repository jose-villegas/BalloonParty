#if UNITY_ENABLE_CHECKS
using BalloonParty.Cheats;
using BalloonParty.Configuration.Level;
using NUnit.Framework;

namespace BalloonParty.Tests.Cheats
{
    [TestFixture]
    public class JumpToScenarioCheatTests
    {
        // Levels 1-4 sky, 5-7 meadow, 8-9 sky, 10-12 water, then sky.
        private static ScenarioKind KindAt(int level)
        {
            if (level <= 4)
            {
                return ScenarioKind.Sky;
            }

            if (level <= 7)
            {
                return ScenarioKind.Ground;
            }

            if (level <= 9)
            {
                return ScenarioKind.Sky;
            }

            return level <= 12 ? ScenarioKind.Water : ScenarioKind.Sky;
        }

        [Test]
        public void FindsTheFirstLevelOfTheNextStretch()
        {
            Assert.AreEqual(10, JumpToScenarioCheat.NextStretchStart(KindAt, 1, ScenarioKind.Water, 100));
            Assert.AreEqual(5, JumpToScenarioCheat.NextStretchStart(KindAt, 1, ScenarioKind.Ground, 100));
        }

        [Test]
        public void FromInsideAStretchOfThatKind_GoesToTheNextOne()
        {
            Assert.AreEqual(8, JumpToScenarioCheat.NextStretchStart(KindAt, 2, ScenarioKind.Sky, 100));
            Assert.AreEqual(13, JumpToScenarioCheat.NextStretchStart(KindAt, 8, ScenarioKind.Sky, 100));
        }

        [Test]
        public void NoStretchWithinTheSearch_IsMinusOne()
        {
            Assert.AreEqual(-1, JumpToScenarioCheat.NextStretchStart(KindAt, 1, ScenarioKind.Water, 5));
            Assert.AreEqual(-1, JumpToScenarioCheat.NextStretchStart(KindAt, 13, ScenarioKind.Ground, 50));
        }
    }
}
#endif
