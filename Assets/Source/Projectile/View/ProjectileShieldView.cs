using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pool;
using BalloonParty.Slots.Grid;
using DG.Tweening;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Projectile.View
{
    internal class ProjectileShieldView : MonoBehaviour
    {
        private const int MaxLayers = 5;

        private static readonly int DissolveProgressId = Shader.PropertyToID("_DissolveProgress");
        private static readonly int ActiveLayersId = Shader.PropertyToID("_ActiveLayers");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private SpriteRenderer _fieldRenderer;

        [Header("VFX")]
        [SerializeField] private ParticleSystem _shieldGainVfxPrefab;
        [SerializeField] private ParticleSystem _shieldLoseVfxPrefab;
        [SerializeField] private ParticleSystem _shieldBounceVfxPrefab;

        [Inject] private IGamePalette _palette;
        [Inject] private IShieldFieldSettings _settings;
        [Inject] private PoolManager _poolManager;
        [Inject] private SlotGrid _grid;

        private readonly CompositeDisposable _disposable = new();
        private readonly float[] _layerDissolve = new float[MaxLayers];

        private MaterialPropertyBlock _block;

        private Tween _dissolveTween;
        private int _previousShieldCount;
        private Color _currentColor = Color.white;
        private IProjectileModel _model;

        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
            }

            if (_fieldRenderer != null)
            {
                _fieldRenderer.enabled = false;
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
                    AnimateShieldChange(count);
                    PlayShieldChangeFx(count);
                    _previousShieldCount = count;
                })
                .AddTo(_disposable);

            // A colourless projectile (fresh launch, or washed by soap) must reset the shield tint
            // to neutral rather than keep the previous projectile's colour.
            model.ColorName
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
            _dissolveTween?.Kill();
            _dissolveTween = null;
            _model = null;
            _previousShieldCount = 0;
            _currentColor = Color.white;

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
            }

            if (_fieldRenderer != null)
            {
                _fieldRenderer.enabled = false;
            }

            PushProperties();
            gameObject.SetActive(false);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            SetImmediateState(_previousShieldCount);
        }

        private void AnimateShieldChange(int newCount)
        {
            _dissolveTween?.Kill();
            _dissolveTween = null;

            var maxVisual = Mathf.Min(newCount, _settings.MaxVisualLayers);
            var prevVisual = Mathf.Min(_previousShieldCount, _settings.MaxVisualLayers);

            if (newCount > _previousShieldCount)
            {
                // Gained shield(s): appear from outermost new layer inward.
                var layerIndex = Mathf.Clamp(maxVisual - 1, 0, MaxLayers - 1);
                _dissolveTween = DOTween.To(
                        () => _layerDissolve[layerIndex],
                        v =>
                        {
                            _layerDissolve[layerIndex] = v;
                            PushProperties();
                        },
                        0f,
                        _settings.AppearSeconds)
                    .SetTarget(this)
                    .SetEase(Ease.OutQuad);
            }
            else if (newCount < _previousShieldCount)
            {
                // Lost shield(s): dissolve the outermost former layer.
                var layerIndex = Mathf.Clamp(prevVisual - 1, 0, MaxLayers - 1);
                _dissolveTween = DOTween.To(
                        () => _layerDissolve[layerIndex],
                        v =>
                        {
                            _layerDissolve[layerIndex] = v;
                            PushProperties();
                        },
                        1f,
                        _settings.DissolveSeconds)
                    .SetTarget(this)
                    .SetEase(Ease.InQuad);
            }

            if (_fieldRenderer != null)
            {
                _fieldRenderer.enabled = newCount > 0;
            }
        }

        private void SetImmediateState(int count)
        {
            if (_settings == null)
            {
                return;
            }

            var maxVisual = Mathf.Min(count, _settings.MaxVisualLayers);

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = i < maxVisual ? 0f : 1f;
            }

            if (_fieldRenderer != null)
            {
                _fieldRenderer.enabled = count > 0;
            }

            PushProperties();
        }

        private void PushProperties()
        {
            if (_fieldRenderer == null || _settings == null || _block == null)
            {
                return;
            }

            _block.SetFloatArray(DissolveProgressId, _layerDissolve);
            _block.SetFloat(ActiveLayersId, _settings.MaxVisualLayers);
            _block.SetColor(ColorId, _currentColor);
            _fieldRenderer.SetPropertyBlock(_block);
        }

        private void PlayShieldChangeFx(int currentCount)
        {
            if (currentCount > _previousShieldCount)
            {
                var lastHit = _model?.LastHitBalloon;
                var gainPosition = lastHit != null
                    ? _grid.IndexToWorldPosition(lastHit.SlotIndex.Value)
                    : transform.position;
                SpawnVfx(_shieldGainVfxPrefab, gainPosition, _currentColor);
            }
            else if (currentCount < _previousShieldCount)
            {
                var direction = _model?.Direction ?? Vector3.up;
                var rotation = Quaternion.LookRotation(Vector3.forward, direction);
                SpawnVfxRotated(_shieldLoseVfxPrefab, transform.position, rotation, _currentColor);
            }
        }

        private void SpawnVfx(ParticleSystem prefab, Vector3 position, Color color)
        {
            if (prefab == null)
            {
                return;
            }

            _poolManager.PlayParticle(prefab, position, color);
        }

        private void SpawnVfxRotated(ParticleSystem prefab, Vector3 position, Quaternion rotation, Color color)
        {
            if (prefab == null)
            {
                return;
            }

            _poolManager.PlayParticle(prefab, position, rotation, color);
        }

        private void UpdateColor(string colorName)
        {
            _currentColor = string.IsNullOrEmpty(colorName)
                ? Color.white
                : _palette.GetColor(colorName);
            PushProperties();
        }
    }
}
