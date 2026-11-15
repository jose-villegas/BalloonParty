using System;
using BalloonParty.Game.Health;
using BalloonParty.Game.Level;
using BalloonParty.Game.Upgrades;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Keeps <see cref="IRunSave" /> at the run's last level boundary: as each level begins (after a level-up's
    ///     transition, and at a retry's reset) it saves what the level begins with, and on a loss it saves the
    ///     game-over screen. A fresh run clears the save. Saving at boundaries only means a resume replays the
    ///     level from its start, with no mid-level board to restore.
    /// </summary>
    internal sealed class RunSaveRecorder : IRunResettable, IStartable, IDisposable
    {
        private readonly IRunSave _save;
        private readonly IRetryState _retryState;
        private readonly ILevelProgress _levelProgress;
        private readonly IRunScore _score;
        private readonly IPlayerHealth _health;
        private readonly IRunSeed _seed;
        private readonly IHeldCards _cards;
        private readonly ISubscriber<RunResetMessage> _resetSubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _transitionSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly CompositeDisposable _subscriptions = new();

        private ResetKind _lastReset;
        private bool _lost;

        // Reads the staged retry before RetryTracker (Score + 10) clears it. The save itself waits for
        // RunResetMessage, once every reset (the retry count among them) has run.
        public int ResetOrder => RunResetOrder.Score + 5;

        public RunSaveRecorder(
            IRunSave save,
            IRetryState retryState,
            ILevelProgress levelProgress,
            IRunScore score,
            IPlayerHealth health,
            IRunSeed seed,
            IHeldCards cards,
            ISubscriber<RunResetMessage> resetSubscriber,
            ISubscriber<LevelTransitionCompletedMessage> transitionSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber)
        {
            _save = save;
            _retryState = retryState;
            _levelProgress = levelProgress;
            _score = score;
            _health = health;
            _seed = seed;
            _cards = cards;
            _resetSubscriber = resetSubscriber;
            _transitionSubscriber = transitionSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
        }

        public void Start()
        {
            _resetSubscriber.Subscribe(_ => OnRunReset()).AddTo(_subscriptions);
            _transitionSubscriber.Subscribe(_ => SaveLevelStart()).AddTo(_subscriptions);
            _gameOverSubscriber.Subscribe(OnGameOver).AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public void ResetRun(int generation)
        {
            if (_retryState.Resuming != null)
            {
                _lastReset = ResetKind.Resume;
            }
            else
            {
                _lastReset = _retryState.RetryLevel > 0 ? ResetKind.Retry : ResetKind.Fresh;
            }
        }

        private void OnRunReset()
        {
            _lost = false;
            switch (_lastReset)
            {
                case ResetKind.Fresh:
                    _save.Clear();
                    break;
                case ResetKind.Retry:
                    // The retry just spent is in the save, so quitting can't hand it back.
                    SaveLevelStart();
                    break;
                case ResetKind.Resume:
                    // Already saved; and a game-over save must stay one until the screen is back.
                    break;
            }
        }

        // A loss deferred past a level-up fires inside the transition's own handlers, so the transition can
        // arrive here after the loss: once lost, only a reset saves a level start again.
        private void SaveLevelStart()
        {
            if (_lost || _health.Current.Value <= 0)
            {
                return;
            }

            var snapshot = Snapshot(_levelProgress.Level.Value);
            _cards.RecordHeld(snapshot, withBonus: true);
            _save.Write(snapshot);
        }

        private void OnGameOver(GameOverMessage message)
        {
            _lost = true;
            if (message.Restored)
            {
                return;
            }

            var snapshot = Snapshot(message.FinalLevel);
            snapshot.OnGameOver = true;
            snapshot.FinalScore = message.FinalScore;
            _cards.RecordHeld(snapshot, withBonus: false);
            _save.Write(snapshot);
        }

        private RunSnapshot Snapshot(int level)
        {
            return new RunSnapshot
            {
                Level = level,
                Seed = _seed.Value,
                LevelStartScore = _score.LevelStartScore,
                Hearts = _health.Current.Value,
                RetriesUsed = _retryState.RetriesUsed
            };
        }

        private enum ResetKind
        {
            Fresh,
            Retry,
            Resume
        }
    }
}
