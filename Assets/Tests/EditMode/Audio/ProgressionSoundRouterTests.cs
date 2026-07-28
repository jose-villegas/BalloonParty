using System;
using BalloonParty.Audio;
using BalloonParty.Audio.Routing;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Shared.Messages;
using MessagePipe;
using NSubstitute;
using NUnit.Framework;
using UniRx;

namespace BalloonParty.Tests.Audio
{
    [TestFixture]
    public class ProgressionSoundRouterTests
    {
        private ISoundPlayer _player;
        private IMelodicContext _melodic;
        private IMessageHandler<StreakChangedMessage> _streakHandler;

        [SetUp]
        public void SetUp()
        {
            _player = Substitute.For<ISoundPlayer>();
            _melodic = Substitute.For<IMelodicContext>();

            var streakSubscriber = CaptureSubscriber<StreakChangedMessage>(h => _streakHandler = h);
            var scoreTrailSubscriber = CaptureSubscriber<ScoreTrailArrivedMessage>(_ => { });
            var levelUpSubscriber = CaptureSubscriber<ScoreLevelUpMessage>(_ => { });
            var levelUpGlowSubscriber = CaptureSubscriber<LevelUpGlowTrailsMessage>(_ => { });
            var levelUpDismissedSubscriber = CaptureSubscriber<LevelUpDismissedMessage>(_ => { });
            var levelTransitionSubscriber = CaptureSubscriber<LevelTransitionCompletedMessage>(_ => { });
            var boardClearSubscriber = CaptureSubscriber<BoardClearMessage>(_ => { });
            var gameOverSubscriber = CaptureSubscriber<GameOverMessage>(_ => { });
            var gameOverDismissedSubscriber = CaptureSubscriber<GameOverDismissedMessage>(_ => { });
            var ascendStartedSubscriber = CaptureSubscriber<LevelAscendStartedMessage>(_ => { });
            var descendStartedSubscriber = CaptureSubscriber<LevelDescendStartedMessage>(_ => { });
            var progressBarCompletedSubscriber = CaptureSubscriber<ProgressBarCompletedMessage>(_ => { });
            var projectileLoadedSubscriber = CaptureSubscriber<ProjectileLoadedMessage>(_ => { });

            var palette = Substitute.For<IGamePalette>();
            palette.ProgressColorNames.Returns(new[] { "Red", "Blue", "Green", "Yellow" });

            var levelProgress = Substitute.For<ILevelProgress>();
            levelProgress.Phase.Returns(new ReactiveProperty<LevelUpPhase>(LevelUpPhase.Playing));

            var router = new ProgressionSoundRouter(
                _player, _melodic, palette, levelProgress, streakSubscriber, scoreTrailSubscriber, levelUpSubscriber,
                levelUpGlowSubscriber, levelUpDismissedSubscriber, levelTransitionSubscriber,
                boardClearSubscriber, gameOverSubscriber, gameOverDismissedSubscriber,
                ascendStartedSubscriber, descendStartedSubscriber, progressBarCompletedSubscriber,
                projectileLoadedSubscriber);
            router.Start();
        }

        [Test]
        public void OnStreakChanged_SetsMelodicStreakAndPlaysStreakStep()
        {
            _streakHandler.Handle(new StreakChangedMessage("Red", 4));

            _melodic.Received(1).SetStreak(4);
            _player.Received(1).Play(GameSoundId.StreakStep, null);
        }

        private static ISubscriber<T> CaptureSubscriber<T>(Action<IMessageHandler<T>> capture)
        {
            var subscriber = Substitute.For<ISubscriber<T>>();
            subscriber
                .Subscribe(
                    Arg.Do(capture),
                    Arg.Any<MessageHandlerFilter<T>[]>())
                .Returns(Substitute.For<IDisposable>());
            return subscriber;
        }
    }
}
