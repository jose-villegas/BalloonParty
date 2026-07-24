using System;
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
        private readonly ISubscriber<StreakChangedMessage> _streakSubscriber;
        private readonly ISubscriber<ScoreTrailArrivedMessage> _scoreTrailSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly ISubscriber<LevelUpGlowTrailsMessage> _levelUpGlowSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _levelTransitionSubscriber;
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<GameOverDismissedMessage> _gameOverDismissedSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        [Inject]
        public ProgressionSoundRouter(ISoundPlayer player, IMelodicContext melodic,
            ISubscriber<StreakChangedMessage> streakSubscriber,
            ISubscriber<ScoreTrailArrivedMessage> scoreTrailSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<LevelUpGlowTrailsMessage> levelUpGlowSubscriber,
            ISubscriber<LevelUpDismissedMessage> levelUpDismissedSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> levelTransitionSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<GameOverDismissedMessage> gameOverDismissedSubscriber)
        {
            _player = player;
            _melodic = melodic;
            _streakSubscriber = streakSubscriber;
            _scoreTrailSubscriber = scoreTrailSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _levelUpGlowSubscriber = levelUpGlowSubscriber;
            _levelUpDismissedSubscriber = levelUpDismissedSubscriber;
            _levelTransitionSubscriber = levelTransitionSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _gameOverDismissedSubscriber = gameOverDismissedSubscriber;
        }

        public void Start()
        {
            _streakSubscriber.Subscribe(OnStreakChanged).AddTo(_subscriptions);
            _scoreTrailSubscriber.Subscribe(OnScoreTrailArrived).AddTo(_subscriptions);
            _levelUpSubscriber.Subscribe(OnLevelUp).AddTo(_subscriptions);
            _levelUpGlowSubscriber.Subscribe(OnLevelUpGlow).AddTo(_subscriptions);
            _levelUpDismissedSubscriber.Subscribe(OnLevelUpDismissed).AddTo(_subscriptions);
            _levelTransitionSubscriber.Subscribe(OnLevelTransition).AddTo(_subscriptions);
            _boardClearSubscriber.Subscribe(OnBoardClear).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(OnGameOver).AddTo(_subscriptions);
            _gameOverDismissedSubscriber.Subscribe(OnGameOverDismissed).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void OnStreakChanged(StreakChangedMessage message)
        {
            _melodic.SetStreak(message.Streak);
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

        private void OnLevelUpGlow(LevelUpGlowTrailsMessage message)
        {
            _player.Play(GameSoundId.LevelUpGlow, null);
        }

        private void OnLevelUpDismissed(LevelUpDismissedMessage message)
        {
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
    }
}
