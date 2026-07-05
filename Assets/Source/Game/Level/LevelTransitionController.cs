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
    ///     The Ascent: on dismissing the level-up popup, clears the board scorelessly, pans the camera
    ///     away, re-populates the new level's actors (statics, then balloons) behind the covered swap,
    ///     and snaps back for the reveal. Holds <see cref="PauseSource.LevelTransition" /> for the whole
    ///     sequence so the thrower and spawn-loss checks stay inert until the reveal is ready.
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

                var ascendDuration = _ascendCinematic.BeginAscend();
                _boardClearPublisher.Publish(default);

                await UniTask.Delay(TimeSpan.FromSeconds(ascendDuration), ignoreTimeScale: true, cancellationToken: ct);

                // Non-balloon stages are placed while the board is still hidden behind the zoomed-out
                // framing, so the instant snap-back below reveals them already in place.
                await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, ct);
                _ascendCinematic.EndAscend();

                await _spawnerCoordinator.RunStagesAsync(s => s == SpawnStage.BalloonActors, ct);
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
