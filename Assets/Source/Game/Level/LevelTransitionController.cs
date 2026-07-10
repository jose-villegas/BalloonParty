using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Cinematics;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Spawner;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     Holds <see cref="PauseSource.LevelTransition" /> for the whole Ascent sequence so the thrower and
    ///     spawn-loss checks stay inert until the reveal is ready.
    /// </summary>
    internal sealed class LevelTransitionController : IStartable, IDisposable
    {
        private readonly CinematicDirector _cinematicDirector;
        private readonly CinematicCameraRig _cameraRig;
        private readonly ICinematicsSettings _cinematicsSettings;
        private readonly GridSpawnerCoordinator _spawnerCoordinator;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly StaticActorSpawner _staticActorSpawner;
        private readonly IReadOnlyList<ITransitionOutgoingContent> _outgoingContent;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly IBoardEffect _boardEffect;
        private readonly RejectedBalloonEffect _overflow;
        private readonly PauseService _pauseService;
        private readonly ILevelProgress _levelProgress;
        private readonly IPublisher<LevelTransitionCompletedMessage> _completedPublisher;
        private readonly CancellationTokenSource _cts = new();

        private LevelAscendCinematic _ascendCinematic;
        private IDisposable _phaseSubscription;

        [Inject]
        internal LevelTransitionController(
            CinematicDirector cinematicDirector,
            CinematicCameraRig cameraRig,
            ICinematicsSettings cinematicsSettings,
            GridSpawnerCoordinator spawnerCoordinator,
            ScenarioContentRoot scenarioRoot,
            StaticActorSpawner staticActorSpawner,
            IReadOnlyList<ITransitionOutgoingContent> outgoingContent,
            BalloonControllerRegistry balloonRegistry,
            IBoardEffect boardEffect,
            RejectedBalloonEffect overflow,
            PauseService pauseService,
            ILevelProgress levelProgress,
            IPublisher<LevelTransitionCompletedMessage> completedPublisher)
        {
            _cinematicDirector = cinematicDirector;
            _cameraRig = cameraRig;
            _cinematicsSettings = cinematicsSettings;
            _spawnerCoordinator = spawnerCoordinator;
            _scenarioRoot = scenarioRoot;
            _staticActorSpawner = staticActorSpawner;
            _outgoingContent = outgoingContent;
            _balloonRegistry = balloonRegistry;
            _boardEffect = boardEffect;
            _overflow = overflow;
            _pauseService = pauseService;
            _levelProgress = levelProgress;
            _completedPublisher = completedPublisher;
        }

        public void Start()
        {
            _ascendCinematic = new LevelAscendCinematic(_cinematicDirector, _cinematicsSettings);

            // The Ascent is driven by the level-up phase, not the dismissal message directly: LevelController
            // flips to Transitioning on dismiss, which fires exactly once per ceremony — so no separate
            // re-entrancy flag is needed, and the trigger order is deterministic.
            _phaseSubscription = _levelProgress.Phase
                .Where(phase => phase == LevelUpPhase.Transitioning)
                .Subscribe(_ => TransitionAsync().Forget());
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _phaseSubscription?.Dispose();
        }

        private async UniTaskVoid TransitionAsync()
        {
            var ct = _cts.Token;

            _pauseService.Pause(PauseSource.LevelTransition);

            try
            {
                // Let any other in-flight cinematic (e.g. HeartDrain) finish first, or TryBeginCinematic skips the descent.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitUntil(() => !Cinematic.IsPlaying, cancellationToken: ct);

                await UniTask.WaitUntil(() => !_overflow.IsOverflowActive, cancellationToken: ct);

                // Reset the staging root to origin before anything reparents onto it (balloons + statics),
                // so their reparent lands at the right offset.
                _scenarioRoot.Transform.position = Vector3.zero;

                // Both the outgoing balloons and the statics reparent under the root, which the descent
                // lifts by this height on its first frame; they subtract it to hold their original spot.
                var exitDrop = _cinematicsSettings.LevelAscend.Height;

                // Detach the old balloons into the outgoing group — off the grid and reparented under the
                // root, so they travel with the descent while the float animates them up.
                _boardEffect.Collect(exitDrop);

                // Un-zoom the camera (the level-up pan-in left it zoomed) over the LevelUpRestore segment's
                // own duration — independent of the board effect, which is now a separate concurrent beat.
                var restoreSeconds = _cinematicsSettings.EntryOf(CinematicState.LevelUpRestore).Rig.TimeScaleCurve.Duration();
                _cameraRig.RestoreTweened(restoreSeconds);

                HoldOutgoingContent(exitDrop);

                // The board effect is a detached, concurrent beat: its balloons are already off the grid and
                // out of logic, so it must NOT gate the run reopening. Fire it and let it finish — and pool
                // its own balloons — on its own clock; only the descent below gates gameplay resuming.
                _boardEffect.PlayAsync(ct).Forget();

                // Clear only the OLD statics (the effect owns the balloons); new ones spawn at origin so
                // PlayAsync's lift/slide carries them into place.
                _staticActorSpawner.ClearStaticActors();
                await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, ct);

                // New level's balloons spawn from the descent's cue (fired at LevelAscend.BalloonSpawnCue),
                // so they reveal near the end of the Ascent. The run reopens the moment this lands, whether
                // or not the detached board effect is still playing out.
                await _ascendCinematic.PlayAsync(_scenarioRoot.Transform, SpawnNewLevelBalloons, ct);
            }
            finally
            {
                ReleaseOutgoingContent();

                // Guarantees the thrower unlocks even if a step above throws or is cancelled.
                _pauseService.Resume(PauseSource.LevelTransition);

                // Return the ceremony to Playing so scoring reopens. In the finally so it always fires,
                // even if the descent bailed or was cancelled.
                _completedPublisher.Publish(default);
            }

            // Fired by the descent near its end: sweep any straggler and spawn the new level's balloons.
            // ClearAll precedes the spawn so slots are free regardless of the pop wave's progress.
            void SpawnNewLevelBalloons()
            {
                _balloonRegistry.ClearAll(playPopVfx: false);
                _spawnerCoordinator.RunStagesAsync(s => s == SpawnStage.BalloonActors, _cts.Token).Forget();
            }
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
    }
}
