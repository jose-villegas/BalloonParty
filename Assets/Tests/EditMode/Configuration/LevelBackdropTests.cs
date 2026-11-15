using BalloonParty.Configuration.Level;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Configuration
{
    [TestFixture]
    public class LevelBackdropTests
    {
        private const int RunSeed = 77;

        private ILevelPacingConfiguration _pacing;

        [SetUp]
        public void SetUp()
        {
            _pacing = Substitute.For<ILevelPacingConfiguration>();
            _pacing.ScenarioForLevel(Arg.Any<int>(), Arg.Any<int>()).Returns(ScenarioKind.Water);
        }

        [Test]
        public void AnOrdinaryLevel_ShowsItsScheduledScenario()
        {
            Assert.AreEqual(ScenarioKind.Water, _pacing.BackdropForLevel(14, RunSeed));
        }

        [Test]
        public void ABonusLevel_ShowsPaper()
        {
            _pacing.IsExtraColorLevel(14, RunSeed).Returns(true);

            Assert.AreEqual(ScenarioKind.Paper, _pacing.BackdropForLevel(14, RunSeed));
        }

        [Test]
        public void TheBonusRoll_IsReadForThisRun()
        {
            _pacing.IsExtraColorLevel(14, RunSeed).Returns(true);

            Assert.AreEqual(ScenarioKind.Water, _pacing.BackdropForLevel(14, RunSeed + 1));
        }
    }
}
