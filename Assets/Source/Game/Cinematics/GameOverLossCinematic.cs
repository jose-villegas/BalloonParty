using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Game.Run;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Spawner;
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
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly GridSpawnerCoordinator _spawnerCoordinator;
        private readonly StaticActorSpawner _staticActorSpawner;
        private readonly IReadOnlyList<ITransitionOutgoingContent> _outgoingContent;
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
            ScenarioContentRoot scenarioRoot,
            GridSpawnerCoordinator spawnerCoordinator,
            StaticActorSpawner staticActorSpawner,
            IReadOnlyList<ITransitionOutgoingContent> outgoingContent,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<GameOverDismissedMessage> dismissedSubscriber)
            : base(director, rig, timeScale, settings)
        {
            _pauseService = pauseService;
            _gate = gate;
            _runController = runController;
            _popWave = popWave;
            _scenarioRoot = scenarioRoot;
            _spawnerCoordinator = spawnerCoordinator;
            _staticActorSpawner = staticActorSpawner;
            _outgoingContent = outgoingContent;
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

            // Skip path (no beat played) — nothing to unwind, so restart the whole board immediately.
            ResumeCinematicPause();
            _runController.RestartRun();
        }

        // Camera-down transition, the mirror of the level-up ascend: the lost level becomes the outgoing
        // group — its balloons detach and its scenery is snapshotted under the root — and rides up and out
        // while the new scenery rises in beneath it. The balloons pop in a wave as they ascend ("gone", vs.
        // the level-up's float-away "survived"). Detaching clears the grid, so the incoming scenery fills
        // fully; balloons arrive once it has settled.
        private async UniTaskVoid RestoreAndRestartAsync()
        {
            var height = Settings.LevelAscend.Height;

            _restoreDone = new UniTaskCompletionSource();
            if (!Runner.TryBeginRestore())
            {
                _restoreDone.TrySetResult();
            }

            // Graduate the lost level to outgoing: the pop wave's collect detaches the balloons off the grid
            // (offset to exit the top), and the scenery is snapshotted the same way. Both ride the root out.
            _scenarioRoot.Transform.position = Vector3.zero;
            _popWave.Collect(-height);
            HoldOutgoingContent(-height);

            // Reset run state only — the board swap is ours. Clear the live scenery (the snapshots carry it
            // out) and stage the new scenery below view; the grid is empty now, so it fills fully.
            _runController.RestartRun(resetBoard: false);
            _pauseService.Pause(PauseSource.Cinematic);
            _staticActorSpawner.ClearStaticActors();

            // Spawn the scenery with the root at origin (its views set their WORLD transform, so spawning
            // while the root is displaced would bake in that offset), THEN drop the root below view to rise.
            await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, _cts.Token);
            _scenarioRoot.Transform.position = new Vector3(0f, -height, 0f);

            // One travel: outgoing rides up and out (balloons popping in a wave), new scenery rises in
            // beneath, and the new balloons spawn from the rise's cue so they arrive as it settles.
            var popTask = _popWave.PlayAsync(_cts.Token);
            await RiseScenarioAsync(height, SpawnNewBalloons, _cts.Token);
            await UniTask.WhenAll(popTask, _restoreDone.Task);

            // Settled — drop the scenery snapshots (the wave pooled its own balloons).
            ReleaseOutgoingContent();
            ResumeCinematicPause();

            void SpawnNewBalloons()
            {
                _spawnerCoordinator.RunStagesAsync(s => s == SpawnStage.BalloonActors, _cts.Token).Forget();
            }
        }

        // Rises the scenario root -height → 0 on unscaled time, paced by the restart rise curve (progress
        // 0→1; ease-in = slow start ramping to full speed). Outgoing content rides up and out; the new
        // scenery, staged below, rises in. Fires onCue once past the balloon-spawn fraction of the rise.
        private async UniTask RiseScenarioAsync(float height, Action onCue, CancellationToken ct)
        {
            var ascend = Settings.LevelAscend;
            var curve = ascend.RestartRiseCurve;
            var duration = curve.Duration();
            var cueTime = duration * Mathf.Clamp01(ascend.RestartBalloonCue);
            var cueFired = false;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                if (!cueFired && elapsed >= cueTime)
                {
                    cueFired = true;
                    onCue?.Invoke();
                }

                var position = _scenarioRoot.Transform.position;
                position.y = Mathf.Lerp(-height, 0f, curve.Evaluate(elapsed));
                _scenarioRoot.Transform.position = position;

                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                elapsed += Time.unscaledDeltaTime;
            }

            if (!cueFired)
            {
                onCue?.Invoke();
            }

            _scenarioRoot.Transform.position = Vector3.zero;
        }

        private void HoldOutgoingContent(float exitDrop)
        {
            for (var i = 0; i < _outgoingContent.Count; i++)
            {
                _outgoingContent[i].HoldOutgoing(_scenarioRoot.Transform, exitDrop);
            }
        }

        private void ReleaseOutgoingContent()
        {
            for (var i = 0; i < _outgoingContent.Count; i++)
            {
                _outgoingContent[i].ReleaseOutgoing();
            }
        }

        // The camera pull-back finished; the restart's balloon spawn waits on this.
        private void OnRestoreEnded()
        {
            _restoreDone?.TrySetResult();
        }

        // Matches the pull-back to the scenery rise so the camera settles as the new level arrives.
        private float RestoreSeconds()
        {
            var rise = Settings.LevelAscend.RestartRiseCurve.Duration();
            return rise > 0f ? rise : _restoreFloorSeconds;
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
