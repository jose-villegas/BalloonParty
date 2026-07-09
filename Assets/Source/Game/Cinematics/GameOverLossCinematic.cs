using System;
using System.Threading;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     The loss ceremony, split around the GameOver screen (mirrors the level-up flow): a slow-mo
    ///     push-in over the lost board holds the camera in while the screen shows; on dismiss the camera
    ///     pulls back, and the run only restarts once that restore ends. Waits out any in-flight cinematic
    ///     first (the heart-drain keeps playing into game-over) and always opens the gate — even when the
    ///     beat can't run — so the screen never soft-locks.
    /// </summary>
    internal sealed class GameOverLossCinematic : CameraRigCinematicProducer
    {
        // Multiple of the pan-in duration to wait for a busy director before giving up and revealing bare.
        private const float DirectorFreeTimeoutFactor = 4f;

        private readonly PauseService _pauseService;
        private readonly GameOverPresentationGate _gate;
        private readonly RunController _runController;
        private readonly BoardPopWave _popWave;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<GameOverDismissedMessage> _dismissedSubscriber;
        private readonly CancellationTokenSource _cts = new();

        private IDisposable _gameOverSubscription;
        private IDisposable _dismissedSubscription;
        private UniTaskCompletionSource _restoreDone;
        private float _holdSeconds;
        private float _restoreFloorSeconds;
        private float _elapsed;
        private bool _awaitingDismiss;

        [Inject]
        internal GameOverLossCinematic(
            CinematicDirector director,
            CinematicCameraRig rig,
            TimeScaleService timeScale,
            ICinematicsSettings settings,
            PauseService pauseService,
            GameOverPresentationGate gate,
            RunController runController,
            BoardPopWave popWave,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<GameOverDismissedMessage> dismissedSubscriber)
            : base(director, rig, timeScale, settings)
        {
            _pauseService = pauseService;
            _gate = gate;
            _runController = runController;
            _popWave = popWave;
            _gameOverSubscriber = gameOverSubscriber;
            _dismissedSubscriber = dismissedSubscriber;
        }

        protected override CameraRigCinematicConfig BuildConfig()
        {
            return new CameraRigCinematicConfig
            {
                PanInState = CinematicState.GameOverLoss,
                RestoreState = CinematicState.GameOverLossRestore,
                RestoreDurationOverride = RestoreSeconds,
                Focus = new PointFocus(() => Vector3.zero),
                OnPanInTick = OnPanInTick,
                OnEnded = OnRestoreEnded,
            };
        }

        protected override void OnStart()
        {
            _holdSeconds = Settings.EntryOf(CinematicState.GameOverLoss).Rig.TimeScaleCurve.Duration();
            _restoreFloorSeconds = Settings.EntryOf(CinematicState.GameOverLossRestore).Rig.TimeScaleCurve.Duration();
            _gameOverSubscription = _gameOverSubscriber.Subscribe(_ => OnGameOver());
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => OnDismissed());
        }

        protected override void OnDispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _gameOverSubscription?.Dispose();
            _dismissedSubscription?.Dispose();
            ResumeCinematicPause();
        }

        private void OnGameOver()
        {
            _gate.Arm();
            _awaitingDismiss = false;
            PlayPanInAsync().Forget();
        }

        private async UniTaskVoid PlayPanInAsync()
        {
            _elapsed = 0f;

            if (!await TryTakeDirectorAsync())
            {
                // No beat played, so there's nothing to restore on dismiss — reveal the screen bare.
                _gate.Open();
                return;
            }

            _pauseService.Pause(PauseSource.Cinematic);
            // The pan-in ticks itself out; OnPanInTick ends it and opens the gate once the hold elapses.
        }

        // Yields until the heart-drain (still winding down into game-over) releases the director, then claims it.
        private async UniTask<bool> TryTakeDirectorAsync()
        {
            var elapsed = 0f;
            var timeout = Mathf.Max(_holdSeconds, 1f) * DirectorFreeTimeoutFactor;
            while (Cinematic.IsPlaying)
            {
                if (elapsed >= timeout)
                {
                    return false;
                }

                await UniTask.Yield(PlayerLoopTiming.Update, _cts.Token);
                elapsed += Time.unscaledDeltaTime;
            }

            return Runner.TryBegin();
        }

        // Holds the push-in for the segment's duration, then ends the pan-in (camera stays in) and reveals the screen.
        private void OnPanInTick(float dt, float _)
        {
            _elapsed += dt;
            if (_elapsed < _holdSeconds || !Runner.IsPanInRunning)
            {
                return;
            }

            Runner.EndPanIn();
            _awaitingDismiss = true;
            _gate.Open();
        }

        private void OnDismissed()
        {
            if (_awaitingDismiss)
            {
                _awaitingDismiss = false;
                RestoreAndRestartAsync().Forget();
                return;
            }

            // Skip path (no beat played) — nothing to unwind, so restart immediately.
            ResumeCinematicPause();
            _runController.RestartRun();
        }

        // Pops the board (like the Ascent) while the camera pulls back, then restarts once both finish.
        private async UniTaskVoid RestoreAndRestartAsync()
        {
            // Snapshot the board before the pop wave (and the restart) empties it. No exit drop — the
            // wave pops in place, with no descending root to compensate for.
            _popWave.Collect(0f);

            _restoreDone = new UniTaskCompletionSource();
            var popTask = _popWave.PlayAsync(_cts.Token);

            // No camera beat to run — don't leave the restore await hanging.
            if (!Runner.TryBeginRestore())
            {
                _restoreDone.TrySetResult();
            }

            await UniTask.WhenAll(popTask, _restoreDone.Task);

            ResumeCinematicPause();
            _runController.RestartRun();
        }

        // The camera pull-back finished; the restart waits on this together with the pop wave.
        private void OnRestoreEnded()
        {
            _restoreDone?.TrySetResult();
        }

        // Matches the pull-back to the pop wave so the camera settles on the last pop; falls back to the
        // authored restore duration when the board is empty (nothing to pop). Sampled after Collect().
        private float RestoreSeconds()
        {
            var pop = _popWave.EstimateSeconds();
            return pop > 0f ? pop : _restoreFloorSeconds;
        }

        private void ResumeCinematicPause()
        {
            if (_pauseService.IsPaused(PauseSource.Cinematic))
            {
                _pauseService.Resume(PauseSource.Cinematic);
            }
        }
    }
}
