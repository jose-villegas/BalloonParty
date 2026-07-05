using System;
using System.Threading;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Cinematics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Spawner;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The Ascent: on dismissing the level-up popup, the remaining balloons pop, the board clears,
    ///     and the camera translates up and back down (as if moving from this scenario's center to a
    ///     new one on top of it) while the new level's statics are placed (already there, hidden, by
    ///     the time we "arrive") and its initial balloons spawn partway through the descent (already
    ///     mid-animation on arrival, not appearing only after). Holds
    ///     <see cref="PauseSource.LevelTransition" /> for the whole sequence so the thrower and
    ///     spawn-loss checks stay inert until the reveal is ready.
    /// </summary>
    internal sealed class LevelTransitionController : IStartable, IDisposable
    {
        private readonly CinematicDirector _cinematicDirector;
        private readonly CinematicCameraRig _cameraRig;
        private readonly ICinematicsSettings _cinematicsSettings;
        private readonly GridSpawnerCoordinator _spawnerCoordinator;
        private readonly RejectedBalloonEffect _overflow;
        private readonly PauseService _pauseService;
        private readonly IPublisher<BoardClearMessage> _boardClearPublisher;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly CancellationTokenSource _cts = new();

        private LevelAscendCinematic _ascendCinematic;
        private IDisposable _dismissedSubscription;

        [Inject]
        internal LevelTransitionController(
            CinematicDirector cinematicDirector,
            CinematicCameraRig cameraRig,
            ICinematicsSettings cinematicsSettings,
            GridSpawnerCoordinator spawnerCoordinator,
            RejectedBalloonEffect overflow,
            PauseService pauseService,
            IPublisher<BoardClearMessage> boardClearPublisher,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber)
        {
            _cinematicDirector = cinematicDirector;
            _cameraRig = cameraRig;
            _cinematicsSettings = cinematicsSettings;
            _spawnerCoordinator = spawnerCoordinator;
            _overflow = overflow;
            _pauseService = pauseService;
            _boardClearPublisher = boardClearPublisher;
            _dismissedSubscriber = dismissedSubscriber;
        }

        public void Start()
        {
            _ascendCinematic = new LevelAscendCinematic(_cinematicDirector, _cameraRig, _cinematicsSettings);
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => TransitionAsync().Forget());
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _dismissedSubscription?.Dispose();
        }

        private async UniTaskVoid TransitionAsync()
        {
            var ct = _cts.Token;
            _pauseService.Pause(PauseSource.LevelTransition);

            try
            {
                // LevelUpCinematic reacts to the same dismiss message with its own restore, on the
                // same shared CinematicCameraRig — let it finish before touching the rig ourselves,
                // or the two producers fight over it (killed tweens, corrupted director bookkeeping).
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitUntil(() => !Cinematic.IsPlaying, cancellationToken: ct);

                await UniTask.WaitUntil(() => !_overflow.IsOverflowActive, cancellationToken: ct);

                // The remaining balloons pop (visible burst) as the board clears; statics are placed
                // right after, hidden the whole time behind the camera's ascent.
                _boardClearPublisher.Publish(new BoardClearMessage(playPopVfx: true));
                await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, ct);

                await _ascendCinematic.PlayAsync(
                    onBalloonSpawnCue: () => _spawnerCoordinator.RunStagesAsync(s => s == SpawnStage.BalloonActors, ct).Forget(),
                    ct);
            }
            finally
            {
                // Guarantees the thrower unlocks even if a step above throws or is cancelled —
                // a stuck PauseSource.LevelTransition means a permanently unthrowable projectile.
                _pauseService.Resume(PauseSource.LevelTransition);
            }
        }
    }
}
