using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Thrower
{
    public class ThrowerController : MonoBehaviour, IStartable
    {
        private ProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        [Inject] private IGameConfiguration _config;
        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;
        private Vector3 _direction = Vector3.up;
        [Inject] private SlotGrid _grid;
        private bool _isMovable;
        [Inject] private IPublisher<ProjectileLoadedMessage> _loadedPublisher;
        [Inject] private IObjectResolver _resolver;
        [Inject] private ThrowerSettings _settings;

        private void Update()
        {
            if (!_isMovable) return;

            UpdateDirection();
            RotateToDirection();
            UpdateLoadedProjectilePosition();
            TryFire();
        }

        public void Start()
        {
            _destroyedSubscriber.Subscribe(_ => Reload());

            // Mirror GameStartedThrowerSpawnSystem: drop in from below then become active
            transform.position = _config.ThrowerSpawnPoint + Vector2.down;
            transform.DOMove(_config.ThrowerSpawnPoint, 1f).OnComplete(() =>
            {
                _isMovable = true;
                LoadProjectile();
            });
        }

        private void UpdateDirection()
        {
            if (!Input.GetMouseButton(0)) return;

            var cam = Camera.main;
            if (cam == null) return;

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
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree) return;

            var spawnPoint = _config.ProjectileSpawnPoint;
            var center = transform.position;
            var angle = Vector3.Angle(_direction, Vector3.right) - 90f;
            var rad = angle * Mathf.Deg2Rad;

            var rotatedX = Mathf.Cos(rad) * (spawnPoint.x - center.x) - Mathf.Sin(rad) * (spawnPoint.y - center.y) +
                           center.x;
            var rotatedY = Mathf.Sin(rad) * (spawnPoint.x - center.x) + Mathf.Cos(rad) * (spawnPoint.y - center.y) +
                           center.y;

            _activeView.transform.position = new Vector3(rotatedX, rotatedY, 0f);
            _activeView.transform.up = _direction;
            _activeProjectile.Direction = _direction;
        }

        private void TryFire()
        {
            if (_activeProjectile == null || _activeView == null || _activeProjectile.IsFree) return;
            if (!Input.GetMouseButtonUp(0)) return;
            if (_grid == null) return;

            // Only fire when all balloons have settled (mirrors ThrowLoadedProjectileSystem)
            for (var col = 0; col < _grid.Columns; col++)
            for (var row = 0; row < _grid.Rows; row++)
                if (!_grid.IsEmpty(col, row) && !_grid.At(new Vector2Int(col, row)).IsStable.Value)
                    return;

            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
        }

        private void LoadProjectile()
        {
            if (_settings.ProjectilePrefab == null) return;

            var instance = _resolver.Instantiate(_settings.ProjectilePrefab, transform.position, Quaternion.identity);
            _activeView = instance.GetComponent<ProjectileView>();

            if (_activeView == null)
            {
                Destroy(instance);
                return;
            }

            _activeProjectile = new ProjectileModel
            {
                Speed = _config.ProjectileSpeed,
                IsFree = false,
                Direction = _direction
            };
            _activeProjectile.ShieldsRemaining.Value = _config.ProjectileStartingShields;

            _activeView.Bind(_activeProjectile);
            _loadedPublisher.Publish(default);
        }

        public void FireImmediate()
        {
            if (_activeProjectile == null || _activeProjectile.IsFree) return;
            _activeProjectile.IsFree = true;
            _activeProjectile.Direction = _direction;
        }

        private void Reload()
        {
            _activeProjectile = null;
            _activeView = null;
            LoadProjectile();
        }
    }
}