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
    ///     The Ascent: on dismissing the level-up popup, the old level's balloons pop in a slow-mo
    ///     diagonal wave and — concurrently — the new level's static content (every actor parented
    ///     under the shared <see cref="ScenarioContentRoot" />) slides down into place while the camera
    ///     stays fixed, as if we'd moved up to a new scenario stacked on top of this one. The one
    ///     transform this controller moves is that root: it spawns the new statics into it, lifts it,
    ///     and slides it back to the origin; the content follows because the cluster views and slot
    ///     markers render relative to their transform. Once every balloon has popped, the new level's
    ///     balloons spawn (animating in while the scenario finishes settling). Holds
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
        private readonly StaticActorSpawner _staticActorSpawner;
        private readonly IReadOnlyList<ITransitionOutgoingContent> _outgoingContent;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly TimeScaleService _timeScale;
        private readonly RejectedBalloonEffect _overflow;
        private readonly PauseService _pauseService;
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
            StaticActorSpawner staticActorSpawner,
            IReadOnlyList<ITransitionOutgoingContent> outgoingContent,
            BalloonControllerRegistry balloonRegistry,
            TimeScaleService timeScale,
            RejectedBalloonEffect overflow,
            PauseService pauseService,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber)
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

                // Un-zoom the camera (the level-up pan-in left it zoomed) in lockstep with the pop.
                _cameraRig.RestoreTweened(EstimatePopWaveSeconds());

                // Hold the outgoing content on the scenario root (at origin now) BEFORE anything clears
                // or pops: cluster views snapshot themselves and the live balloons reparent onto the
                // root, all offset one drop below the incoming content, so the whole old level slides
                // down and out as the new slides in. Must precede the pop wave so every balloon rides
                // out (and pops band-by-band as it goes), and precede the clear so the clusters snapshot
                // while the grid is still populated. Released once the descent settles.
                _scenarioRoot.Transform.position = Vector3.zero;
                HoldOutgoingContent();

                // Start the old level's balloons popping (slow-mo diagonal wave from the two far
                // corners inward) — they pop while sliding out. Runs concurrently with the descent.
                var popTask = PopBalloonsInWaveAsync(ct);

                // Swap in the new scenario's statics and start them descending WHILE the balloons are
                // still popping. Clear only the OLD statics here (the wave owns the balloons); spawn the
                // new ones at the origin so their cluster views/markers settle at true grid positions,
                // then PlayAsync lifts the root on its first frame (before any render) and slides it
                // down — the content rides along.
                _staticActorSpawner.ClearStaticActors();
                await _spawnerCoordinator.RunStagesAsync(s => s < SpawnStage.BalloonActors, ct);
                var descentTask = _ascendCinematic.PlayAsync(_scenarioRoot.Transform, onBalloonSpawnCue: null, ct);

                // Once every balloon has popped, sweep any straggler (item-pending, off-grid) balloon
                // and spawn the new level's balloons — they animate in while the scenario finishes
                // settling, rather than only after it has come to rest.
                await popTask;
                _balloonRegistry.ClearAll(playPopVfx: false);
                _spawnerCoordinator.RunStagesAsync(s => s == SpawnStage.BalloonActors, ct).Forget();

                await descentTask;
            }
            finally
            {
                // The descent has settled (or the sequence bailed) — drop the held outgoing content now
                // that the new scenario is in place.
                ReleaseOutgoingContent();

                // Guarantees the thrower unlocks even if a step above throws or is cancelled —
                // a stuck PauseSource.LevelTransition means a permanently unthrowable projectile.
                _pauseService.Resume(PauseSource.LevelTransition);
            }
        }

        private void HoldOutgoingContent()
        {
            // Same distance PlayAsync lifts the incoming content by, so the outgoing content exits the
            // bottom in lockstep as the new arrives.
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
