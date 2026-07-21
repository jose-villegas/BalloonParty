using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Math;
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
        private const int MaxLayers = 30;

        private static readonly int DissolveProgressId = Shader.PropertyToID("_DissolveProgress");
        private static readonly int RevealProgressId = Shader.PropertyToID("_RevealProgress");
        private static readonly int ActiveLayersId = Shader.PropertyToID("_ActiveLayers");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int VelocityFactorId = Shader.PropertyToID("_VelocityFactor");
        private static readonly int NoiseScrollDirId = Shader.PropertyToID("_NoiseScrollDir");
        private static readonly int ShapeLerpId = Shader.PropertyToID("_ShapeLerp");
        private static readonly int NoiseIntensityId = Shader.PropertyToID("_NoiseIntensity");

        [SerializeField] private SpriteRenderer _fieldRenderer;

        [Header("VFX")]
        [SerializeField] private ParticleSystem _shieldGainVfxPrefab;
        [SerializeField] private ParticleSystem _shieldLoseVfxPrefab;
        [SerializeField] private ParticleSystem _shieldBounceVfxPrefab;

        [Inject] private IGamePalette _palette;
        [Inject] private IShieldFieldSettings _settings;
        [Inject] private IGameConfiguration _gameConfig;
        [Inject] private PoolManager _poolManager;
        [Inject] private SlotGrid _grid;
        [Inject] private ProjectileMotionResolver _motionResolver;

        private readonly CompositeDisposable _disposable = new();
        private readonly float[] _layerDissolve = new float[MaxLayers];
        private readonly float[] _layerReveal = new float[MaxLayers];

        private MaterialPropertyBlock _block;

        private Tween _dissolveTween;
        private int _previousShieldCount;
        private Color _currentColor = Color.white;
        private IProjectileModel _model;

        private DampedSpring2D _springNoise;
        private bool _initialized;
        private float _velFactor;
        private Vector4 _noiseScrollDir;

        private ShieldMorphState _morphState;
        private float _morphTimer;
        private float _shapeLerp = 1f;
        private float _noiseIntensity;

        private enum ShieldMorphState
        {
            Cruising,
            Closing,
            Bracing,
            Opening
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
                _layerReveal[i] = 0f;
            }

            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_model == null || _fieldRenderer == null)
            {
                return;
            }

            if (_block == null || _settings == null)
            {
                return;
            }

            Vector2 currentFacing = transform.up;
            var dt = Time.deltaTime;

            if (!_initialized)
            {
                _springNoise.Reset(currentFacing);
                _initialized = true;
            }

            // Noise direction spring — fast response for scroll direction
            _springNoise.Step(currentFacing, _settings.NoiseSpringFrequency, _settings.NoiseSpringDamping, dt);

            // Shape morph state machine
            UpdateMorphState(dt);

            _velFactor = Mathf.Sqrt(
                Mathf.Clamp01(_model.Speed / Mathf.Max(_settings.MaxVisualSpeed, 1f)));

            // Noise specks: off by default, ramp with cruise taps, full on piercing
            var threshold = _gameConfig != null ? _gameConfig.CruisePiercingTapThreshold : 0;
            if (_model.IsPiercing.Value)
            {
                _noiseIntensity = 1f;
            }
            else if (threshold > 0 && _model.IsCruising.Value)
            {
                _noiseIntensity = Mathf.Clamp01((float)_model.Flight.TotalCruiseTaps / threshold);
            }
            else
            {
                _noiseIntensity = 0f;
            }

            var localNoise = (Vector2)transform.InverseTransformDirection(_springNoise.Position)
                             - Vector2.up;
            localNoise = Vector2.ClampMagnitude(localNoise, 1.5f);
            _noiseScrollDir = new Vector4(localNoise.x, localNoise.y, 0f, 0f);

            WriteAllProperties();
            _fieldRenderer.SetPropertyBlock(_block);
        }

        private void UpdateMorphState(float dt)
        {
            switch (_morphState)
            {
                case ShieldMorphState.Cruising:
                    _shapeLerp = 1f;
                    _morphTimer += dt;
                    if (_morphTimer >= 0.05f)
                    {
                        _morphTimer = 0f;
                        var distance = ComputeWallDistance();
                        if (distance >= 0f && distance < _settings.MorphCloseDistance)
                        {
                            _morphState = ShieldMorphState.Closing;
                        }
                    }

                    break;

                case ShieldMorphState.Closing:
                    _morphTimer += dt;
                    var closeT = Mathf.Clamp01(_morphTimer / Mathf.Max(_settings.MorphCloseDuration, 0.001f));
                    _shapeLerp = 1f - Mathf.SmoothStep(0f, 1f, closeT);
                    if (closeT >= 1f)
                    {
                        _morphState = ShieldMorphState.Bracing;
                        _morphTimer = 0f;
                    }

                    break;

                case ShieldMorphState.Bracing:
                    _shapeLerp = 0f;
                    _morphTimer += dt;
                    if (_morphTimer >= _settings.MorphBraceDuration)
                    {
                        _morphState = ShieldMorphState.Opening;
                        _morphTimer = 0f;
                    }

                    break;

                case ShieldMorphState.Opening:
                    _morphTimer += dt;
                    var openT = Mathf.Clamp01(_morphTimer / Mathf.Max(_settings.MorphOpenDuration, 0.001f));
                    _shapeLerp = Mathf.SmoothStep(0f, 1f, openT);
                    if (openT >= 1f)
                    {
                        // Skip Cruising if already near the next wall
                        var wallDist = ComputeWallDistance();
                        if (wallDist >= 0f && wallDist < _settings.MorphCloseDistance)
                        {
                            _morphState = ShieldMorphState.Closing;
                            _morphTimer = 0f;
                        }
                        else
                        {
                            _morphState = ShieldMorphState.Cruising;
                        }
                    }

                    break;
            }
        }

        private float ComputeWallDistance()
        {
            if (_model == null || _motionResolver == null)
            {
                return -1f;
            }

            var pos = transform.parent != null ? transform.parent.position : transform.position;
            if (!_motionResolver.Walls.TryFindCrossing(pos, _model.Direction, out var crossing, out _))
            {
                return -1f;
            }

            return Vector3.Distance(pos, crossing);
        }

        private void OnDestroy()
        {
            _disposable.Dispose();
        }

        internal void Bind(IProjectileModel model)
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

            model.ColorName
                .Subscribe(UpdateColor)
                .AddTo(_disposable);
        }

        internal void OnBounce()
        {
            if (_settings == null)
            {
                return;
            }

            // Snap to bracing — full circle on impact
            _morphState = ShieldMorphState.Bracing;
            _morphTimer = 0f;
            _shapeLerp = 0f;
        }

        internal void PlayBounceVfx(Vector3 position, Color color)
        {
            SpawnVfx(_shieldBounceVfxPrefab, position, color);
        }

        internal void Reset()
        {
            _disposable.Clear();
            DOTween.Kill(this);
            _dissolveTween = null;
            _model = null;
            _previousShieldCount = 0;
            _currentColor = Color.white;
            _springNoise = default;
            _initialized = false;
            _velFactor = 0f;
            _noiseScrollDir = Vector4.zero;
            _morphState = ShieldMorphState.Cruising;
            _morphTimer = 0f;
            _shapeLerp = 1f;
            _noiseIntensity = 0f;

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
                _layerReveal[i] = 0f;
            }

            if (_fieldRenderer != null && _block != null)
            {
                _block.Clear();
                if (_settings != null)
                {
                    WriteAllProperties();
                }

                _fieldRenderer.SetPropertyBlock(_block);
            }

            gameObject.SetActive(false);
        }

        internal void Show()
        {
            gameObject.SetActive(true);
            AnimateInitialReveal(_previousShieldCount);
        }

        private void AnimateInitialReveal(int count)
        {
            if (_settings == null)
            {
                SetImmediateState(count);
                return;
            }

            var maxVisual = Mathf.Min(count, _settings.MaxVisualLayers);

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = i < maxVisual ? 0f : 1f;
                _layerReveal[i] = 0f;
            }

            var perLayer = _settings.AppearSeconds;
            var stagger = perLayer * 0.3f;

            for (var i = 0; i < maxVisual && i < MaxLayers; i++)
            {
                var idx = i;
                DOTween.To(
                        () => _layerReveal[idx],
                        v => { _layerReveal[idx] = v; },
                        1f,
                        perLayer)
                    .SetTarget(this)
                    .SetEase(Ease.OutQuad)
                    .SetDelay(idx * stagger);
            }
        }

        private void AnimateShieldChange(int newCount)
        {
            DOTween.Kill(this);
            _dissolveTween = null;

            var maxVisual = Mathf.Clamp(newCount, 0, _settings.MaxVisualLayers);
            var prevVisual = Mathf.Clamp(_previousShieldCount, 0, _settings.MaxVisualLayers);

            for (var i = maxVisual; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
                _layerReveal[i] = 0f;
            }

            for (var i = 0; i < maxVisual; i++)
            {
                _layerDissolve[i] = 0f;
                _layerReveal[i] = 1f;
            }

            if (newCount > _previousShieldCount)
            {
                var layerIndex = Mathf.Clamp(maxVisual - 1, 0, MaxLayers - 1);
                _layerDissolve[layerIndex] = 0f;
                _layerReveal[layerIndex] = 0f;

                _dissolveTween = DOTween.To(
                        () => _layerReveal[layerIndex],
                        v => { _layerReveal[layerIndex] = v; },
                        1f,
                        _settings.AppearSeconds)
                    .SetTarget(this)
                    .SetEase(Ease.OutQuad);
            }
            else if (newCount < _previousShieldCount)
            {
                var layerIndex = Mathf.Clamp(prevVisual - 1, 0, MaxLayers - 1);
                _layerDissolve[layerIndex] = 0f;
                _layerReveal[layerIndex] = 1f;

                var isFinal = newCount == 0;
                var duration = isFinal ? _settings.FinalDissolveSeconds : _settings.DissolveSeconds;

                _dissolveTween = DOTween.To(
                        () => _layerDissolve[layerIndex],
                        v => { _layerDissolve[layerIndex] = v; },
                        1f,
                        duration)
                    .SetTarget(this)
                    .SetEase(Ease.InQuad)
                    .OnComplete(() =>
                    {
                        _dissolveTween = null;
                    });
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
                _layerReveal[i] = i < maxVisual ? 1f : 0f;
            }
        }

        private void WriteAllProperties()
        {
            _block.SetFloatArray(DissolveProgressId, _layerDissolve);
            _block.SetFloatArray(RevealProgressId, _layerReveal);
            _block.SetFloat(ActiveLayersId, _settings.MaxVisualLayers);
            _block.SetColor(ColorId, _currentColor);
            _block.SetFloat(VelocityFactorId, _velFactor);
            _block.SetVector(NoiseScrollDirId, _noiseScrollDir);
            _block.SetFloat(ShapeLerpId, _shapeLerp);
            _block.SetFloat(NoiseIntensityId, _noiseIntensity);
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

        private void UpdateColor(string colorName)
        {
            _currentColor = string.IsNullOrEmpty(colorName)
                ? Color.white
                : _palette.GetColor(colorName);
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
    }
}
