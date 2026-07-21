using BalloonParty.Configuration.Effects;
using BalloonParty.Projectile.Model;
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
        private static readonly int DeformDirId = Shader.PropertyToID("_DeformDir");
        private static readonly int VelocityFactorId = Shader.PropertyToID("_VelocityFactor");
        private static readonly int NoiseScrollDirId = Shader.PropertyToID("_NoiseScrollDir");
        private static readonly int SquashMagId = Shader.PropertyToID("_SquashMag");
        private static readonly int SquashStrengthId = Shader.PropertyToID("_SquashStrength");
        private static readonly int SquashNormalId = Shader.PropertyToID("_SquashNormal");
        private static readonly int LeanStrengthYId = Shader.PropertyToID("_LeanStrengthY");

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
        private readonly float[] _layerReveal = new float[MaxLayers];

        private MaterialPropertyBlock _block;

        private Tween _dissolveTween;
        private int _previousShieldCount;
        private Color _currentColor = Color.white;
        private IProjectileModel _model;

        private Vector2 _bounceDeform;
        private DampedSpring2D _leanSpring;
        private DampedSpring2D _springNoise;
        private DampedSpring1D _springSquash;
        private Vector2 _squashNormal;
        private bool _initialized;
        private float _velFactor;
        private Vector4 _noiseScrollDir;
        private Vector4 _deformDir;
        private Vector4 _localSquashNormal;
        private float _squashDisplay;

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

            // Lean spring: targets zero (rest = no deformation). Impulses from OnBounce
            // kick it in local space; it oscillates and settles back to zero naturally.
            _leanSpring.Step(Vector2.zero, _settings.LeanFrequency, _settings.LeanDamping, dt);
            _bounceDeform = _leanSpring.Position;

            // Noise direction spring — fast response for scroll direction
            _springNoise.Step(currentFacing, _settings.NoiseSpringFrequency, _settings.NoiseSpringDamping, dt);

            // Squash spring — decays toward zero (rest = no squash)
            _springSquash.Step(0f, _settings.SquashFrequency, _settings.SquashDamping, dt);

            // Asymmetric envelope: instant attack, configurable slow recovery
            var rawSquash = _springSquash.Position;
            if (Mathf.Abs(rawSquash) > Mathf.Abs(_squashDisplay))
            {
                _squashDisplay = rawSquash;
            }
            else
            {
                var alpha = 1f - Mathf.Exp(-dt / Mathf.Max(_settings.SquashRecoveryTau, 0.001f));
                _squashDisplay = Mathf.Lerp(_squashDisplay, rawSquash, alpha);
            }

            _deformDir = new Vector4(_bounceDeform.x, _bounceDeform.y, 0f, 0f);

            _velFactor = Mathf.Sqrt(
                Mathf.Clamp01(_model.Speed / Mathf.Max(_settings.MaxVisualSpeed, 1f)));

            var localNoise = (Vector2)transform.InverseTransformDirection(_springNoise.Position)
                             - Vector2.up;
            localNoise = Vector2.ClampMagnitude(localNoise, 1.5f);
            _noiseScrollDir = new Vector4(localNoise.x, localNoise.y, 0f, 0f);

            var localSquashNormal = (Vector2)transform.InverseTransformDirection(_squashNormal);
            if (localSquashNormal.sqrMagnitude < 0.001f)
            {
                localSquashNormal = Vector2.up;
            }
            else
            {
                localSquashNormal.Normalize();
            }

            _localSquashNormal = new Vector4(localSquashNormal.x, localSquashNormal.y, 0f, 0f);

            // Single push point — all shader uniforms written here
            WriteAllProperties();
            _fieldRenderer.SetPropertyBlock(_block);
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

            model.ColorName
                .Subscribe(UpdateColor)
                .AddTo(_disposable);
        }

        public void OnBounce(Vector2 oldDirection, Vector2 newDirection)
        {
            if (_settings == null)
            {
                return;
            }

            var oldDir = oldDirection.normalized;
            var newDir = newDirection.normalized;

            // Impact magnitude: 0 (same dir) → 1 (180° reversal)
            var impactMagnitude = (1f - Vector2.Dot(oldDir, newDir)) * 0.5f;

            // Lean direction in world space: from new toward old (backward kick)
            var leanMag = Mathf.Pow(impactMagnitude, _settings.LeanCurve);
            var worldLean = (oldDir - newDir).normalized;

            // Project to local space using the NEW direction as reference frame.
            // transform.up is still the OLD direction at this point (rotation updates after OnBounce),
            // so we build the local frame from newDir manually.
            var localX = new Vector2(newDir.y, -newDir.x);
            var localLean = new Vector2(
                Vector2.Dot(worldLean, localX),
                Vector2.Dot(worldLean, newDir));

            // Kick the lean spring in local space — it oscillates and settles to zero
            _leanSpring.AddImpulse(Vector2.ClampMagnitude(
                localLean * leanMag * _settings.LeanImpulseScale, 2f));

            // Noise spring: kick for scroll direction variation
            _springNoise.AddImpulse(worldLean * _settings.LeanImpulseScale * 2f);

            // Squash: curved to boost grazing hits (SquashCurve < 1)
            var curvedMagnitude = Mathf.Pow(impactMagnitude, _settings.SquashCurve);
            _springSquash.AddImpulse(curvedMagnitude * _settings.SquashImpulseScale);

            // Impact normal: average of incoming/outgoing ≈ wall normal
            var normal = (oldDir - newDir).normalized;
            if (normal.sqrMagnitude > 0.001f)
            {
                _squashNormal = normal;
            }
        }

        public void PlayBounceVfx(Vector3 position, Color color)
        {
            SpawnVfx(_shieldBounceVfxPrefab, position, color);
        }

        public void Reset()
        {
            _disposable.Clear();
            DOTween.Kill(this);
            _dissolveTween = null;
            _model = null;
            _previousShieldCount = 0;
            _currentColor = Color.white;
            _bounceDeform = Vector2.zero;
            _leanSpring = default;
            _springNoise = default;
            _springSquash = default;
            _squashNormal = Vector2.up;
            _initialized = false;
            _velFactor = 0f;
            _noiseScrollDir = Vector4.zero;
            _deformDir = Vector4.zero;
            _localSquashNormal = new Vector4(0f, 1f, 0f, 0f);
            _squashDisplay = 0f;

            for (var i = 0; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
                _layerReveal[i] = 0f;
            }

            // Final push before deactivation — Update() won't run after SetActive(false)
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

        public void Show()
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

            // Stagger each layer's reveal wipe with a small delay per layer
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

            // Snap all layers above the new count to fully dissolved
            for (var i = maxVisual; i < MaxLayers; i++)
            {
                _layerDissolve[i] = 1f;
                _layerReveal[i] = 0f;
            }

            // Ensure all layers below the new count are fully visible
            for (var i = 0; i < maxVisual; i++)
            {
                _layerDissolve[i] = 0f;
                _layerReveal[i] = 1f;
            }

            if (newCount > _previousShieldCount)
            {
                // Gained shield: reveal wipe from apex to tail
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
                // Animate the outermost newly-lost layer
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

        /// <summary>
        /// Writes ALL shader uniforms to the property block.
        /// Only <see cref="Update"/> and <see cref="Reset"/> call SetPropertyBlock;
        /// tween callbacks and subscriptions mutate backing fields only.
        /// </summary>
        private void WriteAllProperties()
        {
            _block.SetFloatArray(DissolveProgressId, _layerDissolve);
            _block.SetFloatArray(RevealProgressId, _layerReveal);
            _block.SetFloat(ActiveLayersId, _settings.MaxVisualLayers);
            _block.SetColor(ColorId, _currentColor);
            _block.SetVector(DeformDirId, _deformDir);
            _block.SetFloat(VelocityFactorId, _velFactor);
            _block.SetVector(NoiseScrollDirId, _noiseScrollDir);
            _block.SetFloat(SquashMagId, _squashDisplay);
            _block.SetFloat(SquashStrengthId, _settings.SquashStrength);
            _block.SetVector(SquashNormalId, _localSquashNormal);
            _block.SetFloat(LeanStrengthYId, _settings.LeanStrengthY);
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
        }
    }
}
