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
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Game.Level
{
    /// <summary>
    ///     The Ascent: on dismissing the level-up popup, the remaining balloons pop, the board clears,
    ///     and the new level's static content — every actor parented under the shared
    ///     <see cref="ScenarioContentRoot" /> — slides down into place as one unit while the camera
    ///     stays fixed, as if we'd moved up to a new scenario stacked on top of this one. The one
    ///     transform this controller cares about is that root: it spawns the new statics into it, lifts
    ///     it, and slides it back to the origin; the content follows because the cluster views and
    ///     slot markers render relative to their transform. The new level's balloons spawn partway
    ///     through the descent (already mid-animation on arrival). Holds
    ///     <see cref="PauseSource.LevelTransition" /> for the whole sequence so the thrower and
    ///     spawn-loss checks stay inert until the reveal is ready.
    /// </summary>
    internal sealed class LevelTransitionController : IStartable, IDisposable
    {
        // Slow-mo timescale while the old level's balloons pop; the pop wave advances one anti-diagonal
        // band per interval. Placeholder tuning (like the Ascent's height/duration) — feel pass pending.
        private const float PopSlowMoTimeScale = 0.35f;
        private const float PopWaveBandSeconds = 0.11f;

        private readonly CinematicDirector _cinematicDirector;
        private readonly CinematicCameraRig _cameraRig;
        private readonly ICinematicsSettings _cinematicsSettings;
        private readonly GridSpawnerCoordinator _spawnerCoordinator;
        private readonly ScenarioContentRoot _scenarioRoot;
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly TimeScaleService _timeScale;
        private readonly RejectedBalloonEffect _overflow;
        private readonly PauseService _pauseService;
        private readonly IPublisher<BoardClearMessage> _boardClearPublisher;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly CancellationTokenSource _cts = new();
        private readonly Dictionary<int, List<IBalloonModel>> _popBands = new();

        private LevelAscendCinematic _ascendCinematic;
        private IDisposable _dismissedSubscription;

        [Inject]
        internal LevelTransitionController(
            CinematicDirector cinematicDirector,
            CinematicCameraRig cameraRig,
            ICinematicsSettings cinematicsSettings,
            GridSpawnerCoordinator spawnerCoordinator,
            ScenarioContentRoot scenarioRoot,
            SlotGrid grid,
            BalloonControllerRegistry balloonRegistry,
            TimeScaleService timeScale,
            RejectedBalloonEffect overflow,
            PauseService pauseService,
            IPublisher<BoardClearMessage> boardClearPublisher,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber)
        {
            _cinematicDirector = cinematicDirector;
            _cameraRig = cameraRig;
            _cinematicsSettings = cinematicsSettings;
            _spawnerCoordinator = spawnerCoordinator;
            _scenarioRoot = scenarioRoot;
            _grid = grid;
            _balloonRegistry = balloonRegistry;
            _timeScale = timeScale;
            _overflow = overflow;
            _pauseService = pauseService;
            _boardClearPublisher = boardClearPublisher;
            _dismissedSubscriber = dismissedSubscriber;
        }

        public void Start()
        {
            _ascendCinematic = new LevelAscendCinematic(_cinematicDirector, _cinematicsSettings);
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
                // Let any other in-flight cinematic (e.g. HeartDrain) finish first — TryBeginCinematic
                // would otherwise fail and skip the descent animation outright rather than playing it.
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
                await UniTask.WaitUntil(() => !Cinematic.IsPlaying, cancellationToken: ct);

                await UniTask.WaitUntil(() => !_overflow.IsOverflowActive, cancellationToken: ct);

                // Un-zoom the camera (the level-up pan-in left it zoomed) in lockstep with the pop —
                // started here, right as the balloons begin popping, and spanning the wave's duration,
                // so the two read as one beat. Unscaled, matching the wave's real (post-slow-mo) length.
                _cameraRig.RestoreTweened(EstimatePopWaveSeconds());

                // The old level's balloons pop in a slow-mo wave from the two far corners inward.
                await PopBalloonsInWaveAsync(ct);

                // Clear whatever's left (static actors, plus any straggler balloon not on the grid) —
                // silently now, the visible pop already happened in the wave above.
                _boardClearPublisher.Publish(new BoardClearMessage(playPopVfx: false));

                // Spawn the new statics while the root is at the origin, so cluster views and markers
                // settle at their true grid positions. PlayAsync then lifts the root on its first
                // frame (before any render) and slides it back down — the content rides along.
                _scenarioRoot.Transform.position = Vector3.zero;
                await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, ct);

                await _ascendCinematic.PlayAsync(
                    _scenarioRoot.Transform,
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

        // Pops every balloon on the board in slow-mo, advancing along anti-diagonals (band = col + row)
        // from BOTH far corners — top-left (band 0) and bottom-right (band max) — inward, so the two
        // fronts meet and finish at the centre. Routes each pop through the registry's side-effect-free
        // teardown (no balance/nudge/score); gameplay is already paused via PauseSource.LevelTransition.
        private async UniTask PopBalloonsInWaveAsync(CancellationToken ct)
        {
            CollectBalloonBands();
            if (_popBands.Count == 0)
            {
                return;
            }

            var maxBand = (_grid.Columns - 1) + (_grid.Rows - 1);

            _timeScale.Claim(TimeScaleSource.LevelTransition, PopSlowMoTimeScale);
            try
            {
                for (var near = 0; near <= maxBand - near; near++)
                {
                    PopBand(near);
                    var far = maxBand - near;
                    if (far != near)
                    {
                        PopBand(far);
                    }

                    // Scaled delay, so the slow-mo also stretches the wave's cadence.
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(PopWaveBandSeconds), ignoreTimeScale: false, cancellationToken: ct);
                }
            }
            finally
            {
                _timeScale.Release(TimeScaleSource.LevelTransition);
            }
        }

        // Wall-clock length of the pop wave: one band-interval per anti-diagonal step (every step
        // waits, empty band or not), stretched by the slow-mo the wave runs under. Used to match the
        // camera un-zoom to the wave. Mirror this if the wave loop's cadence changes.
        private float EstimatePopWaveSeconds()
        {
            var maxBand = (_grid.Columns - 1) + (_grid.Rows - 1);
            var steps = (maxBand / 2) + 1;
            return steps * PopWaveBandSeconds / PopSlowMoTimeScale;
        }

        private void CollectBalloonBands()
        {
            _popBands.Clear();

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
