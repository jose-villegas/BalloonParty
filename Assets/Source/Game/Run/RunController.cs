using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
using UniRx;
using VContainer.Unity;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Owns the run lifecycle. <see cref="EndRun"/> commits the meta record, announces
    ///     the loss and transitions to <see cref="NavigationState.GameOver"/>;
    ///     <see cref="RestartRun"/> resets every <see cref="IRunResettable"/> in order and
    ///     returns to play.
    ///
    ///     A loss is suppressed unless the game is actively in <see cref="NavigationState.Game"/>
    ///     and no loss-blocking cinematic is playing — GameOver and the level-up cinematic must never
    ///     overlap (the heart-drain cinematic is not loss-blocking, so a 0-HP loss fires during it).
    ///     Loss triggers call <see cref="EndRun"/> directly (the dev cheat) or raise an
    ///     <see cref="EndRunRequestedMessage"/> (the player-HP pool, which can't depend on this
    ///     controller without forming a DI cycle through the <see cref="IRunResettable"/> graph).
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

        private IDisposable _subscription;
        private IDisposable _navSubscription;
        private bool _lossPending;
        private int _generation = 1;

        public RunController(
            INavigation navigation,
            ICinematicState cinematic,
            IRunMeta runMeta,
            IRunScore score,
            IPublisher<GameOverMessage> gameOverPublisher,
            IPublisher<RunResetMessage> resetPublisher,
            ISubscriber<EndRunRequestedMessage> endRunSubscriber,
            IEnumerable<IRunResettable> resettables)
        {
            _navigation = navigation;
            _cinematic = cinematic;
            _runMeta = runMeta;
            _score = score;
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
            // A loss arriving mid-level-up is deferred, never dropped — the request is one-shot (the HP
            // pool publishes exactly once at 0), so a silent return here would leave a zombie run.
            if (_cinematic.Has(CinematicTraits.BlocksLoss) || _navigation.Current.Value == NavigationState.LevelUp)
            {
                _lossPending = true;
                return;
            }

            if (_navigation.Current.Value != NavigationState.Game)
            {
                return;
            }

            var level = _score.Level.Value;
            var score = _score.TotalScore.Value;

            _runMeta.RecordRun(level, score);
            _gameOverPublisher.Publish(new GameOverMessage(level, score));
            _navigation.TransitionTo(NavigationState.GameOver);
        }

        public void RestartRun()
        {
            _lossPending = false;
            _generation++;

            foreach (var resettable in _resettables)
            {
                resettable.ResetRun(_generation);
            }

            // Views that can't reset reactively (progress bars) or live outside the reset graph's
            // scope (the thrower's projectile) reset off this signal.
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
