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
    public class ThrowerController : MonoBehaviour
    {
        private readonly List<Vector3> _tracePoints = new();
        private ProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private Vector3 _direction = Vector3.up;
        [Inject] private SlotGrid _grid;
        private bool _isMovable;
        [Inject] private IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        [Inject] private LifetimeScope _parentScope;
        [Inject] private PoolManager _poolManager;
        [Inject] private ThrowerSettings _settings;

        private PredictionTraceCalculator _traceCalculator;
        private PredictionTraceView _traceView;

        private string ProjectilePoolKey => _settings.ProjectileScopePrefab.name;

        private void Start()
        {
            _traceCalculator = new PredictionTraceCalculator(_config);
            _traceView = GetComponentInChildren<PredictionTraceView>(true);

            _poolManager.Register(ProjectilePoolKey,
                new ProjectilePoolChannel(_parentScope, _settings.ProjectileScopePrefab));

            _destroyedSubscriber.Subscribe(_ => Reload());

            transform.position = _config.ThrowerSpawnPoint + Vector2.down;
            transform.DOMove(_config.ThrowerSpawnPoint, 1f).OnComplete(() =>
            {
                _isMovable = true;
                LoadProjectile();
            });
        }

        private void Update()
        {
            if (!_isMovable)
            {
                return;
            }

            UpdateDirection();
            RotateToDirection();
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

            var screenPos = cam.WorldToScreenPoint(transform.position);
            var rawDir = (Input.mousePosition - screenPos).normalized;
            rawDir.z = 0f;
            _direction = rawDir;
        }

        private void RotateToDirection()
        {
            var angle = Vector3.Angle(_direction, Vector3.right) - 90f;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }

        private void UpdateLoadedProjectilePosition()
        {
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree)
            {
                return;
            }

            var spawnPoint = _config.ProjectileSpawnPoint;
            var center = transform.position;
            var angle = Vector3.Angle(_direction, Vector3.right) - 90f;
            var rad = angle * Mathf.Deg2Rad;

            var rotatedX = (Mathf.Cos(rad) * (spawnPoint.x - center.x)) - (Mathf.Sin(rad) * (spawnPoint.y - center.y)) +
                           center.x;
            var rotatedY = (Mathf.Sin(rad) * (spawnPoint.x - center.x)) + (Mathf.Cos(rad) * (spawnPoint.y - center.y)) +
                           center.y;

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
            _traceView?.Clear();
        }

        private void UpdatePredictionTrace()
        {
            if (_traceView == null)
            {
                return;
            }

            // Only show trace while aiming a loaded (not yet fired) projectile
            if (_activeProjectile == null || _activeProjectile.IsFree || !Input.GetMouseButton(0))
            {
                _traceView.Clear();
                return;
            }

            _traceCalculator.Calculate(_activeView.transform.position, _direction, _tracePoints);
            _traceView.SetTrace(_tracePoints);
        }

        private void LoadProjectile()
        {
            _activeView = _poolManager.Get<ProjectileView>(ProjectilePoolKey);
            _activeView.transform.position = transform.position;

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
