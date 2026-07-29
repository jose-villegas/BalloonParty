using System;
using System.Collections.Generic;
using BalloonParty.Game.Level;
using BalloonParty.Prediction;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.GameState;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    internal class ThrowerController : IStartable, ITickable, IDisposable
    {
        private readonly IPredictionTraceConfig _traceConfig;
        private readonly IProjectileFlightConfig _flightConfig;
        private readonly IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<RunResetMessage> _resetSubscriber;
        private readonly ISubscriber<RunRestartCompletedMessage> _restartCompletedSubscriber;
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly ISubscriber<ForceDestroyProjectileMessage> _forceDestroySubscriber;
        private readonly ISubscriber<LevelTransitionCompletedMessage> _levelTransitionCompletedSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
        private readonly ISubscriber<LevelUpAbandonedMessage> _levelUpAbandonedSubscriber;
        private readonly ILevelProgress _levelProgress;
        private readonly PauseService _pauseService;
        private readonly IObjectResolver _resolver;
        private readonly List<Vector3> _tracePoints = new();
        private readonly PoolManager _poolManager;
        private readonly ThrowerSettings _settings;
        private readonly ThrowerView _view;
        private readonly ProjectilePositionProvider _positionProvider;
        private readonly PredictionTraceProvider _traceProvider;
        private readonly CompositeDisposable _subscriptions = new();

        // Cached since Object.name allocates; Reload() hits this twice per shot.
        private readonly string _projectilePoolKey;

        private IWriteableProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        private Vector3 _direction = Vector3.up;
        private bool _isMovable;
        private bool _tracePublished;
        private float _loadElapsed;
        private float _loadDuration;
        private PredictionTraceCalculator _traceCalculator;

        // Set by FireBestShotCheat (auto-fire toggle) to override the aimed direction on the next
        // player-initiated fire. Consumed (cleared) once used. Null means no override.
        internal Vector3? DirectionOverride { get; set; }

        [Inject]
        internal ThrowerController(
            ThrowerView view,
            IPredictionTraceConfig traceConfig,
            IProjectileFlightConfig flightConfig,
            PoolManager poolManager,
            IObjectResolver resolver,
            ThrowerSettings settings,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ProjectileLoadedMessage> loadedPublisher,
            ISubscriber<RunResetMessage> resetSubscriber,
            ISubscriber<RunRestartCompletedMessage> restartCompletedSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<ForceDestroyProjectileMessage> forceDestroySubscriber,
            ISubscriber<LevelTransitionCompletedMessage> levelTransitionCompletedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            ISubscriber<LevelUpAbandonedMessage> levelUpAbandonedSubscriber,
            ILevelProgress levelProgress,
            PauseService pauseService,
            ProjectilePositionProvider positionProvider,
            PredictionTraceProvider traceProvider)
        {
            _view = view;
            _traceConfig = traceConfig;
            _flightConfig = flightConfig;
            _poolManager = poolManager;
            _resolver = resolver;
            _settings = settings;
            _destroyedSubscriber = destroyedSubscriber;
            _loadedPublisher = loadedPublisher;
            _resetSubscriber = resetSubscriber;
            _restartCompletedSubscriber = restartCompletedSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _forceDestroySubscriber = forceDestroySubscriber;
            _levelTransitionCompletedSubscriber = levelTransitionCompletedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _levelUpAbandonedSubscriber = levelUpAbandonedSubscriber;
            _levelProgress = levelProgress;
            _pauseService = pauseService;
            _positionProvider = positionProvider;
            _traceProvider = traceProvider;
            _projectilePoolKey = settings.ProjectilePrefab.name;
        }

        public void Start()
        {
            _traceCalculator = new PredictionTraceCalculator(_traceConfig, _flightConfig);
            _view.SetTraceColor(_traceConfig.PredictionTraceColor);

            _poolManager.Register(_projectilePoolKey,
                new ProjectilePoolChannel(_resolver, _settings.ProjectilePrefab));

            _poolManager.Prewarm(_projectilePoolKey, 2);

            // The spent shot scales away while the fresh one loads — unless a level-up claimed the
            // window, in which case the new shot arrives with the new level (LevelTransitionCompleted).
            _destroyedSubscriber.Subscribe(_ => HandleProjectileDestroyed()).AddTo(_subscriptions);

            // Completing was abandoned (run ending/restarting), so the skipped reload happens now or the
            // thrower stays empty forever.
            _levelUpAbandonedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);

            // Force-destroy: the level controller commands the projectile to die through its canonical
            // death path (board depleted, or the CompletingCap timed out).
            _forceDestroySubscriber.Subscribe(_ => ForceDestroyActiveProjectile()).AddTo(_subscriptions);
            _levelTransitionCompletedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);

            // A shot fired in the very frame the level-up triggers never takes a physics step before the
            // freeze — un-fire it, or the dismissal swap scale-drifts it from the muzzle like a phantom.
            _levelUpSubscriber.Subscribe(_ => UnfireIfNeverFlown()).AddTo(_subscriptions);

            // An immediate restart reloads now; a cinematic-deferred one (the loss→restart camera-down)
            // reloads on RunRestartCompletedMessage instead, so the shot arrives with the settled board.
            _resetSubscriber.Subscribe(OnRunReset).AddTo(_subscriptions);
            _restartCompletedSubscriber.Subscribe(_ => LoadProjectile()).AddTo(_subscriptions);
            _boardClearSubscriber.Subscribe(_ => Reload()).AddTo(_subscriptions);

            // A projectile fired just before loss keeps flying on physics alone; scale it away.
            _gameOverSubscriber.Subscribe(_ => ScaleAwayActiveProjectile()).AddTo(_subscriptions);

            Navigation.Current
                .Where(state => state == NavigationState.Game)
                .Take(1)
                .Subscribe(_ => PlayEntrance())
                .AddTo(_subscriptions);
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        public void Tick()
        {
            if (!_isMovable
                || Navigation.Current.Value != NavigationState.Game
                || _pauseService.IsAnyPaused.Value)
            {
                // A pause or state change mid-aim must take the trace down with it, or the aim line
                // and hit markers linger through the overflow/level-up/loss windows.
                ClearPredictionTrace();
                return;
            }

            UpdateDirection();
            _view.RotateTo(_direction);
            UpdateLoadedProjectilePosition();
            UpdatePredictionTrace();
            TryFire();
        }

        // Editor-tooling entry (Shot Solver window / Fire Best Shot cheat): aim the thrower at the
        // given direction and fire, bypassing mouse input. Snaps the loaded shot to the spawn point
        // first so the launch origin matches the solver's per-angle simulation exactly. Deliberately
        // NOT pause-gated — the cheat console holds PauseSource.Cheat while open, so a pause guard
        // would make the cheat a silent no-op; arming while paused is safe because the pause-gated
        // FixedUpdate keeps the shot still until the menu closes.
        internal void FireAt(Vector3 direction)
        {
            if (!_isMovable || _activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            _direction = direction.normalized;
            _view.RotateTo(_direction);
            _activeView.transform.position = _view.SpawnPointPosition;
            _activeView.transform.rotation = _view.Rotation;
            Fire();
        }

        private void PlayEntrance()
        {
            _view.AnimateEntrance().OnComplete(() =>
            {
                _isMovable = true;
                LoadProjectile();
            });
        }

        private void LoadProjectile()
        {
            _activeView = _poolManager.Get<ProjectileView>(_projectilePoolKey);
            _activeView.transform.position = _view.Position;
            _activeView.transform.rotation = _view.Rotation;

            _activeProjectile = new ProjectileModel
            {
                Speed = _flightConfig.ProjectileSpeed,
                IsFree = false,
                Direction = _direction
            };
            _activeProjectile.ShieldsRemaining.Value = _flightConfig.ProjectileStartingShields;

            // A held shot isn't stepping yet, so nothing has resolved a flight speed for it — seed the one
            // feedback reads (the shield field's ripple) with base speed rather than leaving it at zero.
            _activeProjectile.Flight.CurrentSpeed = _activeProjectile.Speed;

            _activeView.Bind(_activeProjectile);
            _positionProvider.Set(_activeView.transform);
            _loadedPublisher.Publish(new ProjectileLoadedMessage(_activeProjectile));

            _loadElapsed = 0f;
            _loadDuration = _flightConfig.ProjectileLoadDuration;
        }

        private void UnfireIfNeverFlown()
        {
            if (_activeProjectile == null || _activeView == null
                || !_activeProjectile.IsFree || _activeView.HasFlown)
            {
                return;
            }

            _activeProjectile.IsFree = false;
            _positionProvider.SetFree(false);
        }

        // Scales the spent shot away (it returns to the pool only once its disappear finishes) and loads a
        // fresh instance now, so Get() never hands back one still mid-disappear.
        private void HandleProjectileDestroyed()
        {
            ScaleAwayActiveProjectile();

            if (_levelProgress.Phase.Value == LevelUpPhase.Playing)
            {
                LoadProjectile();
            }
        }

        // Force-destroys the projectile through its canonical death path (pierce discharge, state
        // cleanup, ProjectileDestroyedMessage). The subsequent DestroyedMessage triggers
        // HandleProjectileDestroyed which handles the visual cleanup and pool return.
        private void ForceDestroyActiveProjectile()
        {
            if (_activeView == null)
            {
                return;
            }

            _activeView.ForceDestroy();
        }

        // Scales the shot away and pools it, without loading a replacement (game-over reloads only on restart).
        private void ScaleAwayActiveProjectile()
        {
            if (_activeView == null)
            {
                return;
            }

            var view = _activeView;
            _positionProvider.Clear();
            _activeProjectile = null;
            _activeView = null;
            view.PlayDisappear(() => _poolManager.Return(_projectilePoolKey, view));
        }

        private void OnRunReset(RunResetMessage message)
        {
            // Only an immediate restart reloads here; a cinematic-deferred board waits for
            // RunRestartCompletedMessage so the shot lands with the settled board, not mid-transition.
            if (message.BoardReset)
            {
                Reload();
            }
        }

        private void Reload()
        {
            // Tick is gated during resets, so a trace left active at reset time would strand a stale marker.
            ClearPredictionTrace();
            _positionProvider.Clear();

            if (_activeView != null)
            {
                _poolManager.Return(_projectilePoolKey, _activeView);
            }

            _activeProjectile = null;
            _activeView = null;
            LoadProjectile();
        }

        private void TryFire()
        {
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            if (!_view.FireReleased)
            {
                return;
            }

            if (DirectionOverride.HasValue)
            {
                _direction = DirectionOverride.Value;
                DirectionOverride = null;
            }

            Fire();
        }

        private void Fire()
        {
            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
            _positionProvider.SetFree(true);
            ClearPredictionTrace();
            _view.PlayRecoil(_direction);
        }

        private void UpdateDirection()
        {
            if (!_view.IsAiming)
            {
                return;
            }

            if (_view.TryGetAimDirection(out var direction))
            {
                _direction = direction;
            }
        }

        private void UpdateLoadedProjectilePosition()
        {
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            if (_loadElapsed < _loadDuration)
            {
                _loadElapsed += Time.deltaTime;
                var t = Mathf.Clamp01(_loadElapsed / _loadDuration);
                var eased = DOVirtual.EasedValue(0f, 1f, t, Ease.OutBack);
                _activeView.transform.position = Vector3.Lerp(_view.Position, _view.SpawnPointPosition, eased);
            }
            else
            {
                _activeView.transform.position = _view.SpawnPointPosition;
            }

            _activeView.transform.rotation = _view.Rotation;
            _activeProjectile.Direction = _direction;
        }

        private void UpdatePredictionTrace()
        {
            if (_activeProjectile == null || _activeProjectile.IsFree || !_view.IsAiming)
            {
                ClearPredictionTrace();
                return;
            }

            _traceCalculator.Calculate(_activeView.transform.position, _direction, _tracePoints);
            _view.SetTrace(_tracePoints);
            _traceProvider.SetTrace(_tracePoints);
            _tracePublished = true;
        }

        // Idempotent so the every-tick not-aiming path doesn't re-issue LineRenderer clears and
        // provider version bumps.
        private void ClearPredictionTrace()
        {
            if (!_tracePublished)
            {
                return;
            }

            _view.ClearTrace();
            _traceProvider.Clear();
            _tracePublished = false;
        }
    }
}
