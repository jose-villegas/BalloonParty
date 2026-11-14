using BalloonParty.Item.Snipe;
using BalloonParty.Shared.Pause;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;

namespace BalloonParty.Tests.Item
{
    [TestFixture]
    public class SnipeLanceLinesTests
    {
        private PauseService _pause;

        [SetUp]
        public void SetUp()
        {
            _pause = new PauseService(
                Substitute.For<IPublisher<PausedMessage>>(), Substitute.For<IPublisher<ResumedMessage>>());
        }

        [Test]
        public void HoldsStrokes_Unpaused_KeepsDrawing()
        {
            Assert.IsFalse(SnipeLanceLines.HoldsStrokes(_pause));
        }

        [Test]
        public void HoldsStrokes_CheatConsoleOpen_Holds()
        {
            _pause.Pause(PauseSource.Cheat);

            Assert.IsTrue(SnipeLanceLines.HoldsStrokes(_pause));
        }

        // A line drawn right before a level-up fades through its cinematic and the ascent instead of hanging
        // over the board at full strength until gameplay resumes.
        [TestCase(PauseSource.Cinematic)]
        [TestCase(PauseSource.LevelUp)]
        [TestCase(PauseSource.LevelTransition)]
        public void HoldsStrokes_UnderALevelUpOrCinematicPause_KeepsFading(PauseSource source)
        {
            _pause.Pause(source);

            Assert.IsFalse(SnipeLanceLines.HoldsStrokes(_pause));
        }
    }
}
