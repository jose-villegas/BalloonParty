using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Game.Run;
using BalloonParty.Slots.Actor;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Slots
{
    /// <summary>
    ///     What the level being laid out says about its mushrooms' placement (<see cref="MushroomPlacementSource" />):
    ///     the layout's seed and the balloon spawner's reserved rows.
    /// </summary>
    [TestFixture]
    public class MushroomPlacementSourceTests
    {
        private ILevelPacingConfiguration _pacing;
        private IRunSeed _runSeed;
        private IActiveLevelParameters _level;
        private IBalloonsConfiguration _balloons;
        private MushroomPlacementSource _source;

        [SetUp]
        public void SetUp()
        {
            _pacing = Substitute.For<ILevelPacingConfiguration>();
            _pacing.SeedForLevel(Arg.Any<int>()).Returns(call => 1000 + (call.Arg<int>() * 37));
            _runSeed = Substitute.For<IRunSeed>();
            _runSeed.Value.Returns(555);
            _level = Substitute.For<IActiveLevelParameters>();
            _level.Level.Returns(6);
            _balloons = Substitute.For<IBalloonsConfiguration>();
            _source = new MushroomPlacementSource(_pacing, _runSeed, _level, _balloons);
        }

        [Test]
        public void LayoutSeed_IsTheSame_ForTheSameLevelOfTheSameRun()
        {
            Assert.AreEqual(_source.LayoutSeed, _source.LayoutSeed, "a retry stands the mushrooms where they stood");
        }

        [Test]
        public void LayoutSeed_ChangesWithTheLevel_AndWithTheRun()
        {
            var levelSix = _source.LayoutSeed;

            _level.Level.Returns(7);
            var levelSeven = _source.LayoutSeed;
            _runSeed.Value.Returns(556);
            var anotherRun = _source.LayoutSeed;

            Assert.AreNotEqual(levelSix, levelSeven);
            Assert.AreNotEqual(levelSeven, anotherRun);
        }

        // The spawner reads the mushrooms' count off IActiveLevelParameters.Current. The seed is asked for the
        // level those parameters were resolved for, so the two cannot name different levels.
        [Test]
        public void LayoutSeed_IsAskedForTheLevelTheActiveParametersWereResolvedFor()
        {
            _level.Level.Returns(9);

            _ = _source.LayoutSeed;

            _pacing.Received(1).SeedForLevel(9);
            _pacing.DidNotReceive().SeedForLevel(6);
        }

        [Test]
        public void BottomRowsReserved_AreTheRowsTheBalloonSpawnerKeepsFree()
        {
            _balloons.PopSpawnFreeRows.Returns(3);

            Assert.AreEqual(3, _source.BottomRowsReserved);
        }
    }
}
