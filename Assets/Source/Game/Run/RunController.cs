using System.Collections.Generic;
using System.Linq;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using MessagePipe;

namespace BalloonParty.Game.Run
{
    /// <summary>
    ///     Owns the run lifecycle. <see cref="EndRun"/> commits the meta record, announces
    ///     the loss and transitions to <see cref="NavigationState.GameOver"/>;
    ///     <see cref="RestartRun"/> resets every <see cref="IRunResettable"/> in order and
    ///     returns to play.
    ///
    ///     A loss is suppressed unless the game is actively in <see cref="NavigationState.Game"/>
    ///     and no cinematic is playing — GameOver and the level-up cinematic must never overlap.
    ///     During Phase 1 the only caller is a cheat; Phase 2's breach detector calls the same
    ///     <see cref="EndRun"/> seam.
    /// </summary>
    internal class RunController
    {
        private readonly ICinematicState _cinematic;
        private readonly IPublisher<GameOverMessage> _gameOverPublisher;
        private readonly INavigation _navigation;
        private readonly IReadOnlyList<IRunResettable> _resettables;
        private readonly IRunMeta _runMeta;
        private readonly IRunScore _score;

        private int _generation = 1;

        public RunController(
            INavigation navigation,
            ICinematicState cinematic,
            IRunMeta runMeta,
            IRunScore score,
            IPublisher<GameOverMessage> gameOverPublisher,
            IEnumerable<IRunResettable> resettables)
        {
            _navigation = navigation;
            _cinematic = cinematic;
            _runMeta = runMeta;
            _score = score;
            _gameOverPublisher = gameOverPublisher;
            _resettables = resettables.OrderBy(r => r.ResetOrder).ToArray();
        }

        public void EndRun()
        {
            if (_cinematic.IsPlaying || _navigation.Current.Value != NavigationState.Game)
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

            _navigation.TransitionTo(NavigationState.Game);
        }
    }
}
