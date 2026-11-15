using System.Collections.Generic;
using BalloonParty.Configuration.Level;
using BalloonParty.Game.Level;
using BalloonParty.Shared.GameState;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Game
{
    [TestFixture]
    public class BonusLevelCueTests
    {
        private ReactiveProperty<NavigationState> _navigation;
        private ReactiveProperty<int> _level;
        private ILevelParameters _parameters;
        private BonusLevelCue _cue;
        private List<int> _started;

        [SetUp]
        public void SetUp()
        {
            _navigation = new ReactiveProperty<NavigationState>(NavigationState.Launch);
            _level = new ReactiveProperty<int>(1);
            _parameters = Substitute.For<ILevelParameters>();
            var navigation = Substitute.For<INavigation>();
            navigation.Current.Returns(_navigation);
            var levelParams = Substitute.For<IActiveLevelParameters>();
            levelParams.Current.Returns(_ => _parameters);
            var progress = Substitute.For<ILevelProgress>();
            progress.Level.Returns(_level);
            _cue = new BonusLevelCue(navigation, levelParams, progress);
            _started = new List<int>();
            _cue.Started.Subscribe(_started.Add);
            _cue.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _cue.Dispose();
        }

        [Test]
        public void PlayBeginningOnAPurpleLevel_AnnouncesIt()
        {
            Purple(13);

            _navigation.Value = NavigationState.Game;

            CollectionAssert.AreEqual(new[] { 13 }, _started);
        }

        [Test]
        public void AnOrdinaryLevel_AnnouncesNothing()
        {
            _navigation.Value = NavigationState.Game;

            CollectionAssert.IsEmpty(_started);
        }

        [Test]
        public void PlayResumingOnTheSameLevel_DoesNotAnnounceItAgain()
        {
            Purple(13);
            _navigation.Value = NavigationState.Game;

            _navigation.Value = NavigationState.LevelUp;
            _navigation.Value = NavigationState.Game;

            CollectionAssert.AreEqual(new[] { 13 }, _started);
        }

        [Test]
        public void ARetry_AnnouncesTheLevelAgain()
        {
            Purple(13);
            _navigation.Value = NavigationState.Game;
            _navigation.Value = NavigationState.GameOver;

            _cue.ResetRun(2);
            _navigation.Value = NavigationState.Game;

            CollectionAssert.AreEqual(new[] { 13, 13 }, _started);
        }

        [Test]
        public void TheNextPurpleLevel_IsAnnouncedAfterItsAscent()
        {
            Purple(13);
            _navigation.Value = NavigationState.Game;
            _navigation.Value = NavigationState.LevelUp;
            _parameters.IsExtraColorLevel.Returns(false);
            _level.Value = 14;
            _navigation.Value = NavigationState.Game;
            _navigation.Value = NavigationState.LevelUp;

            Purple(15);
            _navigation.Value = NavigationState.Game;

            CollectionAssert.AreEqual(new[] { 13, 15 }, _started);
        }

        private void Purple(int level)
        {
            _level.Value = level;
            _parameters.IsExtraColorLevel.Returns(true);
        }
    }
}
