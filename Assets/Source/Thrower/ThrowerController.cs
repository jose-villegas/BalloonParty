using System.Collections.Generic;
using BalloonParty.Prediction;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    public class ThrowerController : IStartable, ITickable
    {
        private readonly IGameConfiguration _config;
        private readonly ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private readonly SlotGrid _grid;
        private readonly IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        private readonly LifetimeScope _parentScope;
        private readonly PoolManager _poolManager;
        private readonly ThrowerSettings _settings;
        private readonly List<Vector3> _tracePoints = new();
        private readonly ThrowerView _view;

        private IWriteableProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        private Vector3 _direction = Vector3.up;
        private bool _isMovable;
        private PredictionTraceCalculator _traceCalculator;

        private string ProjectilePoolKey => _settings.ProjectileScopePrefab.name;

        [Inject]
        public ThrowerController(
            ThrowerView view,
            IGameConfiguration config,
            SlotGrid grid,
            PoolManager poolManager,
            LifetimeScope parentScope,
            ThrowerSettings settings,
            ISubscriber<ProjectileDestroyedMessage> destroyedSubscriber,
            IPublisher<ProjectileLoadedMessage> loadedPublisher)
        {
            _view = view;
            _config = config;
            _grid = grid;
            _poolManager = poolManager;
            _parentScope = parentScope;
            _settings = settings;
            _destroyedSubscriber = destroyedSubscriber;
            _loadedPublisher = loadedPublisher;
        }

        public void Start()
        {
            _traceCalculator = new PredictionTraceCalculator(_config);

            _poolManager.Register(ProjectilePoolKey,
                new ProjectilePoolChannel(_parentScope, _settings.ProjectileScopePrefab));

            _destroyedSubscriber.Subscribe(_ => Reload());

            _view.Position = _config.ThrowerSpawnPoint + Vector2.down;
            _view.AnimateEntrance(_config.ThrowerSpawnPoint, 1f).OnComplete(() =>
            {
                _isMovable = true;
                LoadProjectile();
            });
        }

        public void Tick()
        {
            if (!_isMovable)
            {
                return;
            }

            UpdateDirection();
            _view.RotateTo(_direction);
            UpdateLoadedProjectilePosition();
            UpdatePredictionTrace();
            TryFire();
        }

        private void UpdateDirection()
        {
            if (!Input.GetMouseButton(0))
            {
                return;
            }

            var cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            var screenPos = cam.WorldToScreenPoint(_view.Position);
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

            var spawnPoint = _config.ProjectileSpawnPoint;
            var center = _view.Position;
            var angle = Vector3.Angle(_direction, Vector3.right) - 90f;
            var rad = angle * Mathf.Deg2Rad;

            var rotatedX = (Mathf.Cos(rad) * (spawnPoint.x - center.x)) -
                (Mathf.Sin(rad) * (spawnPoint.y - center.y)) + center.x;
            var rotatedY = (Mathf.Sin(rad) * (spawnPoint.x - center.x)) +
                           (Mathf.Cos(rad) * (spawnPoint.y - center.y)) + center.y;

            _activeView.transform.position = new Vector3(rotatedX, rotatedY, 0f);
            _activeView.transform.up = _direction;
            _activeProjectile.Direction = _direction;
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

            if (!_grid.AllBalloonsStable())
            {
                return;
            }

            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
            _view.ClearTrace();
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

        private void LoadProjectile()
        {
            _activeView = _poolManager.Get<ProjectileView>(ProjectilePoolKey);
            _activeView.transform.position = _view.Position;

            _activeProjectile = new ProjectileModel
            {
                Speed = _config.ProjectileSpeed,
                IsFree = false,
                Direction = _direction
            };
            _activeProjectile.ShieldsRemaining.Value = _config.ProjectileStartingShields;

            _activeView.Bind(_activeProjectile);
            _loadedPublisher.Publish(new ProjectileLoadedMessage(_activeProjectile));
        }

        private void Reload()
        {
            if (_activeView != null)
            {
                _poolManager.Return(ProjectilePoolKey, _activeView);
            }

            _activeProjectile = null;
            _activeView = null;
            LoadProjectile();
        }
    }
}
