using System;
using System.Collections.Generic;
using System.Linq;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;
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
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public void EndRun()
        {
            if (_cinematic.Has(CinematicTraits.BlocksLoss) || _navigation.Current.Value != NavigationState.Game)
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
    }
}
