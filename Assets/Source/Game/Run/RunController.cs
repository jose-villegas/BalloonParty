using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Game.Level;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Owns the run lifecycle — a loss is suppressed unless in <see cref="NavigationState.Game"/> with no loss-blocking cinematic playing, so GameOver and the level-up cinematic never overlap.
    /// </summary>
    internal class RunController : IStartable, IDisposable
    {
        private readonly ICinematicState _cinematic;
        private readonly ISubscriber<EndRunRequestedMessage> _endRunSubscriber;
        private readonly IPublisher<GameOverMessage> _gameOverPublisher;
        private readonly IPublisher<RunResetMessage> _resetPublisher;
        private readonly INavigation _navigation;
        private readonly IReadOnlyList<IRunResettable> _resettables;
        private readonly IRunMeta _runMeta;
        private readonly IRunScore _score;
        private readonly ILevelProgress _levelProgress;

        private IDisposable _subscription;
        private IDisposable _navSubscription;
        private bool _lossPending;
        private int _generation = 1;

        public RunController(
            INavigation navigation,
            ICinematicState cinematic,
            IRunMeta runMeta,
            IRunScore score,
            ILevelProgress levelProgress,
            IPublisher<GameOverMessage> gameOverPublisher,
            IPublisher<RunResetMessage> resetPublisher,
            ISubscriber<EndRunRequestedMessage> endRunSubscriber,
            IEnumerable<IRunResettable> resettables)
        {
            _navigation = navigation;
            _cinematic = cinematic;
            _runMeta = runMeta;
            _score = score;
            _levelProgress = levelProgress;
            _gameOverPublisher = gameOverPublisher;
            _resetPublisher = resetPublisher;
            _endRunSubscriber = endRunSubscriber;
            _resettables = resettables.OrderBy(r => r.ResetOrder).ToArray();
        }

        public void Start()
        {
            _subscription = _endRunSubscriber.Subscribe(_ => EndRun());
            _navSubscription = _navigation.Current.Subscribe(OnNavigationChanged);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _navSubscription?.Dispose();
        }

        public void EndRun()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // Dev cheat (BlockLevelUpCheat) is a level lock: while on, the run can't end — no loss (and no
            // manual end) — so you can sit on a level indefinitely. Toggle off to restore normal loss.
            if (BalloonParty.Cheats.CheatState.BlockLevelUp)
            {
                return;
            }
#endif

            // Deferred, never dropped — the loss request is one-shot.
            if (_cinematic.Has(CinematicTraits.BlocksLoss) || _navigation.Current.Value == NavigationState.LevelUp)
            {
                _lossPending = true;
                return;
            }

            if (_navigation.Current.Value != NavigationState.Game)
            {
                return;
            }

            var level = _levelProgress.Level.Value;
            var score = _score.TotalScore.Value;

            _runMeta.RecordRun(level, score);
            _gameOverPublisher.Publish(new GameOverMessage(level, score));
            _navigation.TransitionTo(NavigationState.GameOver);
        }

        // resetBoard: false resets only run state (score/level/health/counters), leaving the board to a
        // transition cinematic that swaps it in itself (holds the outgoing actors, spawns the incoming).
        public void RestartRun(bool resetBoard = true)
        {
            _lossPending = false;
            _generation++;

            foreach (var resettable in _resettables)
            {
                if (!resetBoard && resettable is IBoardResettable)
                {
                    continue;
                }

                resettable.ResetRun(_generation);
            }

            // For views that can't reset reactively or live outside the reset graph's scope.
            _resetPublisher.Publish(default);

            _navigation.TransitionTo(NavigationState.Game);
        }

        // Fires a loss that was deferred by the level-up gates the moment play resumes.
        private void OnNavigationChanged(NavigationState state)
        {
            if (state == NavigationState.Game && _lossPending)
            {
                _lossPending = false;
                EndRun();
            }
        }
    }
}
