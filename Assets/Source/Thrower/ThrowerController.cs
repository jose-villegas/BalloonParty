using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Model;
using BalloonParty.Projectile.View;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;

namespace BalloonParty.Thrower
{
    public class ThrowerController : MonoBehaviour, IStartable
    {
        [Inject] private ThrowerSettings _settings;
        [Inject] private IGameConfiguration _config;
        [Inject] private IObjectResolver _resolver;
        [Inject] private SlotGrid _grid;
        [Inject] private ISubscriber<ProjectileDestroyedMessage> _destroyedSubscriber;

        private ProjectileModel _activeProjectile;
        private ProjectileView _activeView;
        private Vector3 _direction = Vector3.up;
        private bool _isMovable;

        public void Start()
        {
            _destroyedSubscriber.Subscribe(_ => Reload());

            // Mirror GameStartedThrowerSpawnSystem: drop in from below then become active
            transform.position = (Vector2)_config.ThrowerSpawnPoint + Vector2.down;
            transform.DOMove(_config.ThrowerSpawnPoint, 1f).OnComplete(() =>
            {
                _isMovable = true;
                LoadProjectile();
            });
        }

        private void Update()
        {
            if (!_isMovable) return;

            UpdateDirection();
            RotateToDirection();
            UpdateLoadedProjectilePosition();
            TryFire();
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

            var rotatedX = Mathf.Cos(rad) * (spawnPoint.x - center.x) - Mathf.Sin(rad) * (spawnPoint.y - center.y) + center.x;
            var rotatedY = Mathf.Sin(rad) * (spawnPoint.x - center.x) + Mathf.Cos(rad) * (spawnPoint.y - center.y) + center.y;

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
            for (int col = 0; col < _grid.Columns; col++)
                for (int row = 0; row < _grid.Rows; row++)
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
                UnityEngine.Object.Destroy(instance);
                return;
            }

            _activeProjectile = new ProjectileModel
            {
                Speed = _config.ProjectileSpeed,
                ShieldsRemaining = _config.ProjectileStartingShields,
                IsFree = false,
                Direction = _direction,
            };

            _activeView.Bind(_activeProjectile);
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


