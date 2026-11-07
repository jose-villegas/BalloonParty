using System;
using BalloonParty.Configuration.Palette;
using BalloonParty.Game.Level;
using BalloonParty.Game.Score;
using BalloonParty.Projectile;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Audio.Routing
{
    internal sealed class ProgressionSoundRouter : IStartable, IDisposable
    {
        private readonly ISoundPlayer _player;
        private readonly IMelodicContext _melodic;
        private readonly IGamePalette _palette;
        private readonly ILevelProgress _levelProgress;
        private readonly ISubscriber<StreakChangedMessage> _streakSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _scoreTrailSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly ISubscriber<LevelUpFillTrailsMessage> _levelUpFillSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _levelTransitionSubscriber;
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<GameOverDismissedMessage> _gameOverDismissedSubscriber;
        private readonly ISubscriber<LevelAscendStartedMessage> _ascendStartedSubscriber;
        private readonly ISubscriber<LevelDescendStartedMessage> _descendStartedSubscriber;
        private readonly ISubscriber<ProgressBarCompletedMessage> _progressBarCompletedSubscriber;
        private readonly IProjectileRegistry _projectiles;
        private readonly IColorStreak _colorStreak;
        private readonly CompositeDisposable _subscriptions = new();

        [Inject]
        public ProgressionSoundRouter(ISoundPlayer player, IMelodicContext melodic, IGamePalette palette,
            ILevelProgress levelProgress,
            ISubscriber<StreakChangedMessage> streakSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> scoreTrailSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<LevelUpFillTrailsMessage> levelUpFillSubscriber,
            ISubscriber<LevelUpDismissedMessage> levelUpDismissedSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> levelTransitionSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<GameOverDismissedMessage> gameOverDismissedSubscriber,
            ISubscriber<LevelAscendStartedMessage> ascendStartedSubscriber,
            ISubscriber<LevelDescendStartedMessage> descendStartedSubscriber,
            ISubscriber<ProgressBarCompletedMessage> progressBarCompletedSubscriber,
            IProjectileRegistry projectiles,
            IColorStreak colorStreak)
        {
            _player = player;
            _melodic = melodic;
            _palette = palette;
            _levelProgress = levelProgress;
            _streakSubscriber = streakSubscriber;
            _scoreTrailSubscriber = scoreTrailSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _levelUpFillSubscriber = levelUpFillSubscriber;
            _levelUpDismissedSubscriber = levelUpDismissedSubscriber;
            _levelTransitionSubscriber = levelTransitionSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _gameOverDismissedSubscriber = gameOverDismissedSubscriber;
            _ascendStartedSubscriber = ascendStartedSubscriber;
            _descendStartedSubscriber = descendStartedSubscriber;
            _progressBarCompletedSubscriber = progressBarCompletedSubscriber;
            _projectiles = projectiles;
            _colorStreak = colorStreak;
        }

        public void Start()
        {
            _streakSubscriber.Subscribe(OnStreakChanged).AddTo(_subscriptions);
            _scoreTrailSubscriber.Subscribe(OnScoreTrailArrived).AddTo(_subscriptions);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(_subscriptions);
            _levelUpFillSubscriber.Subscribe(OnLevelUpFill).AddTo(_subscriptions);
            _levelUpDismissedSubscriber.Subscribe(OnLevelUpDismissed).AddTo(_subscriptions);
            _levelTransitionSubscriber.Subscribe(OnLevelTransition).AddTo(_subscriptions);
            _boardClearSubscriber.Subscribe(OnBoardClear).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(OnGameOver).AddTo(_subscriptions);
            _gameOverDismissedSubscriber.Subscribe(OnGameOverDismissed).AddTo(_subscriptions);
            _ascendStartedSubscriber.Subscribe(OnAscendStarted).AddTo(_subscriptions);
            _descendStartedSubscriber.Subscribe(OnDescendStarted).AddTo(_subscriptions);
            _progressBarCompletedSubscriber.Subscribe(OnProgressBarCompleted).AddTo(_subscriptions);

            _levelProgress.Phase
                .Where(p => p == LevelUpPhase.Completing)
                .Subscribe(_ => PlayLevelCompletePop())
                .AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        // The melodic streak follows the best run still in play, not the message: with several balls,
        // one ball's reset must not drop the melody under another ball's climbing streak.
        private void OnStreakChanged(StreakChangedMessage message)
        {
            _melodic.SetStreak(_colorStreak.HighestStreak);
            _player.Play(GameSoundId.StreakStep, null);
        }

        private void OnScoreTrailArrived(ScoreTrailArrivedMessage message)
        {
            _player.Play(GameSoundId.ScoreChime, message.WorldPosition);
        }

        private void OnLevelUp(ScoreLevelUpMessage message)
        {
            _player.Play(GameSoundId.LevelUp, null);
        }

        private void OnLevelUpFill(LevelUpFillTrailsMessage message)
        {
            _player.Play(GameSoundId.LevelUpFill, null);
        }

        private void OnLevelUpDismissed(LevelUpDismissedMessage message)
        {
            // The confirm is the popup's button; a level-up that passed in play pressed nothing.
            if (message.PassedInPlay)
            {
                return;
            }

            _player.Play(GameSoundId.UiConfirm, null);
        }

        private void OnLevelTransition(LevelTransitionCompletedMessage message)
        {
            _player.Play(GameSoundId.LevelTransition, null);
        }

        private void OnBoardClear(BoardClearMessage message)
        {
            _player.Play(GameSoundId.BoardClear, null);
        }

        private void OnGameOver(GameOverMessage message)
        {
            _player.Play(GameSoundId.GameOver, null);
        }

        private void OnGameOverDismissed(GameOverDismissedMessage message)
        {
            _player.Play(GameSoundId.UiConfirm, null);
        }

        private void OnAscendStarted(LevelAscendStartedMessage message)
        {
            _player.Play(GameSoundId.LevelAscend, null);
        }

        private void OnDescendStarted(LevelDescendStartedMessage message)
        {
            _player.Play(GameSoundId.LevelDescend, null);
        }

        private void OnProgressBarCompleted(ProgressBarCompletedMessage message)
        {
            _player.Play(GameSoundId.UiProgressComplete, null,
                semitoneOffset: MusicalPitchExtensions.ColorRootOffset(_palette.ProgressColorNames, message.ColorName));
        }

        private void PlayLevelCompletePop()
        {
            // Completing is claimed mid-flight by the pop that filled the last bar: the latest ball's colour.
            var colorName = _projectiles.ShotOrCurrent(null)?.ColorName.Value;
            var offset = string.IsNullOrEmpty(colorName)
                ? 0
                : MusicalPitchExtensions.ColorRootOffset(_palette.ProgressColorNames, colorName);
            _player.Play(GameSoundId.LevelCompletePop, null, semitoneOffset: offset);
        }
    }
}
