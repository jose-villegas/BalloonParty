using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Slots;
using DG.Tweening;
using UniRx;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile.View
{
    public class ProjectileShieldView : MonoBehaviour
    {
        [SerializeField] private List<SpriteRenderer> _shields;
        [SerializeField] [Range(0f, 1f)] private float _alpha;
        [SerializeField] private float _colorDuration;

        [Header("Scaling")] [SerializeField] private float _scaleDuration;

        [SerializeField] private Vector2 _scaleIncrements;

        [Header("VFX")] [SerializeField] private ParticleSystem _shieldGainVfxPrefab;

        [SerializeField] private ParticleSystem _shieldLoseVfxPrefab;
        [SerializeField] private ParticleSystem _shieldBounceVfxPrefab;

        [Inject] private IGameConfiguration _config;
        [Inject] private PoolManager _poolManager;
        [Inject] private SlotGrid _grid;

        private readonly CompositeDisposable _disposable = new();

        private int _previousShieldCount;
        private Color _currentColor = Color.white;
        private IProjectileModel _model;

        private void Awake()
        {
            foreach (var shield in _shields)
            {
                shield.transform.localScale = Vector3.zero;
            }

            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
        }

        public void Bind(IProjectileModel model)
        {
            _model = model;
            _previousShieldCount = model.ShieldsRemaining.Value;

            model.ShieldsRemaining
                .Skip(1)
                .Subscribe(count =>
                {
                    UpdateShieldVisuals(count);
                    PlayShieldChangeFx(count);
                    _previousShieldCount = count;
                })
                .AddTo(_disposable);

            model.ColorName
                .Where(c => !string.IsNullOrEmpty(c))
                .Subscribe(UpdateColor)
                .AddTo(_disposable);
        }

        public void PlayBounceVfx(Vector3 position, Color color)
        {
            SpawnVfx(_shieldBounceVfxPrefab, position, color);
        }

        public void Reset()
        {
            _disposable.Clear();
            _model = null;
            _previousShieldCount = 0;
            foreach (var shield in _shields)
            {
                shield.transform.localScale = Vector3.zero;
                shield.DOKill();
            }

            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            UpdateShieldVisuals(_previousShieldCount);
        }

        private void UpdateShieldVisuals(int count)
        {
            for (var i = 0; i < _shields.Count; i++)
            {
                var target = i < count
                    ? Vector3.one + (Vector3.right * _scaleIncrements.x * i) + (Vector3.up * _scaleIncrements.y * i)
                    : Vector3.zero;

                _shields[i].transform.DOScale(target, _scaleDuration);
            }
        }

        private void UpdateColor(string colorName)
        {
            _currentColor = _config.BalloonColor(colorName);
            var targetColor = new Color(_currentColor.r, _currentColor.g, _currentColor.b, _alpha);

            foreach (var shield in _shields)
            {
                if (shield != null)
                {
                    shield.DOColor(targetColor, _colorDuration);
                }
            }
        }

        private void PlayShieldChangeFx(int currentCount)
        {
            if (currentCount > _previousShieldCount)
            {
                var lastHit = _model?.LastHitBalloon;
                var gainPosition = lastHit != null
                    ? _grid.IndexToWorldPosition(lastHit.SlotIndex.Value)
                    : transform.position;
                SpawnVfx(_shieldGainVfxPrefab, gainPosition, CurrentColor());
            }
            else if (currentCount < _previousShieldCount)
            {
                SpawnVfx(_shieldLoseVfxPrefab, transform.position, CurrentColor());
            }
        }

        private Color CurrentColor()
        {
            return _currentColor;
        }

        private void SpawnVfx(ParticleSystem prefab, Vector3 position, Color color)
        {
            if (prefab == null)
            {
                return;
            }

            var key = prefab.name;
            var effect = _poolManager.GetOrRegister(key, () => new ParticlePoolChannel(prefab.gameObject));
            effect.Play(position, color, () => _poolManager.Return(key, effect));
        }
    }
}
