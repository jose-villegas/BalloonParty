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
    internal class ThrowerController : IStartable, ITickable
    {
        private readonly IGameConfiguration _config;
        private readonly IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly ISubscriber<RunResetMessage> _resetSubscriber;
        private readonly PauseService _pauseService;
        private readonly IObjectResolver _resolver;
        private readonly List<Vector3> _tracePoints = new();
        private readonly PoolManager _poolManager;
        private readonly ThrowerSettings _settings;
        private readonly ThrowerView _view;
        private readonly ProjectilePositionProvider _positionProvider;

        private IWriteableProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        private Vector3 _direction = Vector3.up;
        private bool _isMovable;
        private float _loadElapsed;
        private float _loadDuration;
        private PredictionTraceCalculator _traceCalculator;
        private Camera _camera;

        private string ProjectilePoolKey => _settings.ProjectilePrefab.name;

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
            PauseService pauseService,
            ProjectilePositionProvider positionProvider)
        {
            _view = view;
            _config = config;
            _poolManager = poolManager;
            _resolver = resolver;
            _settings = settings;
            _destroyedSubscriber = destroyedSubscriber;
            _loadedPublisher = loadedPublisher;
            _resetSubscriber = resetSubscriber;
            _pauseService = pauseService;
            _positionProvider = positionProvider;
        }

        public void Start()
        {
            _traceCalculator = new PredictionTraceCalculator(_config);

            _poolManager.Register(ProjectilePoolKey,
                new ProjectilePoolChannel(_resolver, _settings.ProjectilePrefab));

            _poolManager.Prewarm(ProjectilePoolKey, 2);

            _destroyedSubscriber.Subscribe(_ => Reload());

            // A restart resets every other system; swap the carried-over projectile for a fresh one
            // so its shield count (and position) start from the configured default.
            _resetSubscriber.Subscribe(_ => Reload());

            Navigation.Current
                .Where(state => state == NavigationState.Game)
                .Take(1)
                .Subscribe(_ => PlayEntrance());
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
            _activeView = _poolManager.Get<ProjectileView>(ProjectilePoolKey);
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

        private void Reload()
        {
            _positionProvider.Clear();

            if (_activeView != null)
            {
                _poolManager.Return(ProjectilePoolKey, _activeView);
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

            if (!Input.GetMouseButtonUp(0))
            {
                return;
            }

            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
            _positionProvider.SetFree(true);
            _view.ClearTrace();
        }

        private void UpdateDirection()
        {
            if (!Input.GetMouseButton(0))
            {
                return;
            }

            _camera ??= Camera.main;
            if (_camera == null)
            {
                return;
            }

            var screenPos = _camera.WorldToScreenPoint(_view.Position);
            var rawDir = (Input.mousePosition - screenPos).normalized;
            rawDir.z = 0f;
            _direction = rawDir;
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
            if (_activeProjectile == null || _activeProjectile.IsFree || !Input.GetMouseButton(0))
            {
                _view.ClearTrace();
                return;
            }

            _traceCalculator.Calculate(_activeView.transform.position, _direction, _tracePoints);
            _view.SetTrace(_tracePoints);
        }
    }
}
