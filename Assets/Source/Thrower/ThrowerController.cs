using System;
using System.Collections.Generic;
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
        private readonly IGameConfiguration _config;
        private readonly IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<RunResetMessage> _resetSubscriber;
        private readonly ISubscriber<BoardClearMessage> _boardClearSubscriber;
        private readonly ISubscriber<LevelUpDismissedMessage> _levelUpDismissedSubscriber;
        private readonly ISubscriber<ScoreLevelUpMessage> _levelUpSubscriber;
        private readonly ISubscriber<GameOverMessage> _gameOverSubscriber;
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
        private float _loadElapsed;
        private float _loadDuration;
        private PredictionTraceCalculator _traceCalculator;

        [Inject]
        internal ThrowerController(
            ThrowerView view,
            IGameConfiguration config,
            PoolManager poolManager,
            IObjectResolver resolver,
            ThrowerSettings settings,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ProjectileLoadedMessage> loadedPublisher,
            ISubscriber<RunResetMessage> resetSubscriber,
            ISubscriber<BoardClearMessage> boardClearSubscriber,
            ISubscriber<LevelUpDismissedMessage> levelUpDismissedSubscriber,
            ISubscriber<ScoreLevelUpMessage> levelUpSubscriber,
            ISubscriber<GameOverMessage> gameOverSubscriber,
            PauseService pauseService,
            ProjectilePositionProvider positionProvider,
            PredictionTraceProvider traceProvider)
        {
            _view = view;
            _config = config;
            _poolManager = poolManager;
            _resolver = resolver;
            _settings = settings;
            _destroyedSubscriber = destroyedSubscriber;
            _loadedPublisher = loadedPublisher;
            _resetSubscriber = resetSubscriber;
            _boardClearSubscriber = boardClearSubscriber;
            _levelUpDismissedSubscriber = levelUpDismissedSubscriber;
            _levelUpSubscriber = levelUpSubscriber;
            _gameOverSubscriber = gameOverSubscriber;
            _pauseService = pauseService;
            _positionProvider = positionProvider;
            _traceProvider = traceProvider;
            _projectilePoolKey = settings.ProjectilePrefab.name;
        }

        public void Start()
        {
            _traceCalculator = new PredictionTraceCalculator(_config);
            _view.SetTraceColor(_config.PredictionTraceColor);

            _poolManager.Register(_projectilePoolKey,
                new ProjectilePoolChannel(_resolver, _settings.ProjectilePrefab));

            _poolManager.Prewarm(_projectilePoolKey, 2);

            // The spent shot scales away (returns to the pool only when that finishes) while a fresh instance
            // loads at once — so the thrower never reuses a shot still mid-disappear.
            _destroyedSubscriber.Subscribe(_ => SwapActiveProjectile()).AddTo(_subscriptions);
            _levelUpDismissedSubscriber.Subscribe(_ => SwapActiveProjectile()).AddTo(_subscriptions);

            // A shot fired in the very frame the level-up triggers never takes a physics step before the
            // freeze — un-fire it, or the dismissal swap scale-drifts it from the muzzle like a phantom.
            _levelUpSubscriber.Subscribe(_ => UnfireIfNeverFlown()).AddTo(_subscriptions);

            // Restart carries over the old projectile; reload so it resets to config defaults.
            _resetSubscriber.Subscribe(_ => Reload()).AddTo(_subscriptions);
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
                Speed = _config.ProjectileSpeed,
                IsFree = false,
                Direction = _direction
            };
            _activeProjectile.ShieldsRemaining.Value = _config.ProjectileStartingShields;

            _activeView.Bind(_activeProjectile);
            _positionProvider.Set(_activeView.transform);
            _loadedPublisher.Publish(new ProjectileLoadedMessage(_activeProjectile));

            _loadElapsed = 0f;
            _loadDuration = _config.ProjectileLoadDuration;
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
        private void SwapActiveProjectile()
        {
            ScaleAwayActiveProjectile();
            LoadProjectile();
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

        private void Reload()
        {
            // Tick is gated during resets, so a trace left active at reset time would strand a stale marker.
            _traceProvider.Clear();
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

            Fire();
        }

        private void Fire()
        {
            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
            _positionProvider.SetFree(true);
            _view.ClearTrace();
            _traceProvider.Clear();
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
                _view.ClearTrace();
                _traceProvider.Clear();
                return;
            }

            _traceCalculator.Calculate(_activeView.transform.position, _direction, _tracePoints);
            _view.SetTrace(_tracePoints);
            _traceProvider.SetTrace(_tracePoints);
        }
    }
}
