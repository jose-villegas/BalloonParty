using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Cinematics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
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
        // Placeholder tuning (like the Ascent's height/duration) — feel pass pending.
        private const float PopSlowMoTimeScale = 0.35f;
        private const float PopWaveBandSeconds = 0.11f;

        private readonly CinematicDirector _cinematicDirector;
        private readonly CinematicCameraRig _cameraRig;
        private readonly ICinematicsSettings _cinematicsSettings;
        private readonly GridSpawnerCoordinator _spawnerCoordinator;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly SlotGrid _grid;
        private readonly StaticActorSpawner _staticActorSpawner;
        private readonly IReadOnlyList<ITransitionOutgoingContent> _outgoingContent;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly TimeScaleService _timeScale;
        private readonly RejectedBalloonEffect _overflow;
        private readonly PauseService _pauseService;
        private readonly ILevelProgress _levelProgress;
        private readonly IPublisher<LevelTransitionCompletedMessage> _completedPublisher;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<int, List<IBalloonModel>> _popBands = new();

        private int _minPopBand;
        private int _maxPopBand;

        private LevelAscendCinematic _ascendCinematic;
        private IDisposable _phaseSubscription;

        [Inject]
        internal LevelTransitionController(
            CinematicDirector cinematicDirector,
            CinematicCameraRig cameraRig,
            ICinematicsSettings cinematicsSettings,
            GridSpawnerCoordinator spawnerCoordinator,
            ScenarioContentRoot scenarioRoot,
            SlotGrid grid,
            StaticActorSpawner staticActorSpawner,
            IReadOnlyList<ITransitionOutgoingContent> outgoingContent,
            BalloonControllerRegistry balloonRegistry,
            TimeScaleService timeScale,
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
            _grid = grid;
            _staticActorSpawner = staticActorSpawner;
            _outgoingContent = outgoingContent;
            _balloonRegistry = balloonRegistry;
            _timeScale = timeScale;
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.Log("[Ascent] transition starting (phase → Transitioning)");
#endif

            _pauseService.Pause(PauseSource.LevelTransition);

            try
            {
                // Let any other in-flight cinematic (e.g. HeartDrain) finish first, or TryBeginCinematic skips the descent.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitUntil(() => !Cinematic.IsPlaying, cancellationToken: ct);

                await UniTask.WaitUntil(() => !_overflow.IsOverflowActive, cancellationToken: ct);

                // Snapshot bands now, while the grid is still fully populated.
                CollectBalloonBands();

                // Un-zoom the camera (the level-up pan-in left it zoomed) in lockstep with the pop.
                _cameraRig.RestoreTweened(EstimatePopWaveSeconds());

                // Must precede the pop wave (clusters snapshot here) and the clear (grid still populated).
                _scenarioRoot.Transform.position = Vector3.zero;
                HoldOutgoingContent();

                // Old balloons pop while sliding out, concurrently with the descent.
                var popTask = PopBalloonsInWaveAsync(ct);

                // Clear only the OLD statics (the wave owns the balloons); new ones spawn at origin so
                // PlayAsync's lift/slide carries them into place.
                _staticActorSpawner.ClearStaticActors();
                await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, ct);

                // New level's balloons spawn from the descent's cue (fired at the LevelAscend rig's
                // PanWeight fraction), so they reveal near the end of the Ascent rather than right after
                // the pop wave. Keep PanWeight late enough that the cue follows the pop wave.
                var descentTask = _ascendCinematic.PlayAsync(_scenarioRoot.Transform, SpawnNewLevelBalloons, ct);

                await popTask;
                await descentTask;
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

        private void HoldOutgoingContent()
        {
            // Same distance PlayAsync lifts the incoming content, so outgoing exits in lockstep.
            var exitDrop = _cinematicsSettings.EntryOf(CinematicState.LevelAscend).Rig.ZoomAmount;
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

        // Pops balloons band-by-band along anti-diagonals from both far corners inward.
        private async UniTask PopBalloonsInWaveAsync(CancellationToken ct)
        {
            if (_popBands.Count == 0)
            {
                return;
            }

            _timeScale.Claim(TimeScaleSource.LevelTransition, PopSlowMoTimeScale);
            try
            {
                // Sweep from the outermost POPULATED bands, not the grid corners, which may be empty.
                for (int near = _minPopBand, far = _maxPopBand; near <= far; near++, far--)
                {
                    PopBand(near);
                    if (far != near)
                    {
                        PopBand(far);
                    }

                    // Scaled delay so slow-mo also stretches the wave's cadence.
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(PopWaveBandSeconds), ignoreTimeScale: false, cancellationToken: ct);
                }
            }
            finally
            {
                _timeScale.Release(TimeScaleSource.LevelTransition);
            }
        }

        // Wall-clock length of the pop wave; mirror this if the wave loop's cadence changes.
        private float EstimatePopWaveSeconds()
        {
            if (_popBands.Count == 0)
            {
                return 0f;
            }

            var steps = ((_maxPopBand - _minPopBand) / 2) + 1;
            return steps * PopWaveBandSeconds / PopSlowMoTimeScale;
        }

        private void CollectBalloonBands()
        {
            _popBands.Clear();
            _minPopBand = int.MaxValue;
            _maxPopBand = int.MinValue;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var balloon = _grid.ActorAt<IWriteableBalloonModel>(new Vector2Int(col, row));
                    if (balloon == null)
                    {
                        continue;
                    }

                    var band = col + row;
                    if (!_popBands.TryGetValue(band, out var models))
                    {
                        models = new List<IBalloonModel>();
                        _popBands[band] = models;
                    }

                    models.Add(balloon);
                    _minPopBand = Mathf.Min(_minPopBand, band);
                    _maxPopBand = Mathf.Max(_maxPopBand, band);
                }
            }
        }

        private void PopBand(int band)
        {
            if (!_popBands.TryGetValue(band, out var models))
            {
                return;
            }

            for (var i = 0; i < models.Count; i++)
            {
                _balloonRegistry.TryPopSingle(models[i]);
            }
        }
    }
}
