using System;
using System.Threading;
using BalloonParty.Balloon.Spawner;
using BalloonParty.Configuration;
using BalloonParty.Game.Cinematics;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Messages;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Actor;
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
    ///     and the new level's static actors are spawned above the board and slide down into place —
    ///     as if we'd moved up to a new scenario stacked on top of this one, without ever moving the
    ///     camera. The new level's initial balloons spawn partway through the descent (already
    ///     mid-animation on arrival, not appearing only after). Holds
    ///     <see cref="PauseSource.LevelTransition" /> for the whole sequence so the thrower and
    ///     spawn-loss checks stay inert until the reveal is ready.
    /// </summary>
    internal sealed class LevelTransitionController : IStartable, IDisposable
    {
        private readonly CinematicDirector _cinematicDirector;
        private readonly ICinematicsSettings _cinematicsSettings;
        private readonly GridSpawnerCoordinator _spawnerCoordinator;
        private readonly StaticActorSpawner _staticActorSpawner;
        private readonly RejectedBalloonEffect _overflow;
        private readonly PauseService _pauseService;
        private readonly IPublisher<BoardClearMessage> _boardClearPublisher;
        private readonly ISubscriber<LevelUpDismissedMessage> _dismissedSubscriber;
        private readonly CancellationTokenSource _cts = new();

        private LevelAscendCinematic _ascendCinematic;
        private Transform _stagingRoot;
        private IDisposable _dismissedSubscription;

        [Inject]
        internal LevelTransitionController(
            CinematicDirector cinematicDirector,
            ICinematicsSettings cinematicsSettings,
            GridSpawnerCoordinator spawnerCoordinator,
            StaticActorSpawner staticActorSpawner,
            RejectedBalloonEffect overflow,
            PauseService pauseService,
            IPublisher<BoardClearMessage> boardClearPublisher,
            ISubscriber<LevelUpDismissedMessage> dismissedSubscriber)
        {
            _cinematicDirector = cinematicDirector;
            _cinematicsSettings = cinematicsSettings;
            _spawnerCoordinator = spawnerCoordinator;
            _staticActorSpawner = staticActorSpawner;
            _overflow = overflow;
            _pauseService = pauseService;
            _boardClearPublisher = boardClearPublisher;
            _dismissedSubscriber = dismissedSubscriber;
        }

        public void Start()
        {
            _ascendCinematic = new LevelAscendCinematic(_cinematicDirector, _cinematicsSettings);
            _stagingRoot = new GameObject("AscentStagingRoot").transform;
            _dismissedSubscription = _dismissedSubscriber.Subscribe(_ => TransitionAsync().Forget());
        }

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
            _dismissedSubscription?.Dispose();

            if (_stagingRoot != null)
            {
                UnityEngine.Object.Destroy(_stagingRoot.gameObject);
            }
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

                // The remaining balloons pop (visible burst) as the board clears.
                _boardClearPublisher.Publish(new BoardClearMessage(playPopVfx: true));

                // Statics spawn already offset above the board (parented under the staging root),
                // hidden above frame — the descent below is what slides them into place.
                var height = _cinematicsSettings.EntryOf(CinematicState.LevelAscend).Rig.ZoomAmount;
                _stagingRoot.position = new Vector3(0f, height, 0f);
                _staticActorSpawner.SpawnStaticActorsInto(_stagingRoot);

                await _ascendCinematic.PlayAsync(
                    _stagingRoot,
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
