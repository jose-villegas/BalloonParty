using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Projectile.View
{
    public class ProjectileView : MonoBehaviour, IPoolable
    {
        private static int BalloonsLayer = -1;

        [Header("Glow")] [SerializeField] private SpriteRenderer _glowRenderer;

        [SerializeField] [Range(0f, 1f)] private float _glowAlpha = 0.5f;
        [SerializeField] private float _glowColorDuration = 0.2f;
        [Tooltip("Full palette loops per second the glow cycles through while the rainbow buff is active.")]
        [SerializeField] [Min(0f)] private float _rainbowGlowSpeed = 1.5f;

        [Header("Scene Light")]
        [Tooltip("Radius of the light this shot casts into the scene-light field. Keep small — it's a bullet.")]
        [SerializeField] [Min(0f)] private float _lightRadius = 0.6f;
        [SerializeField] [Min(0f)] private float _lightIntensity = 1.5f;

        [Inject] private IGameConfiguration _config;
        [Inject] private IGamePalette _palette;
        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private IPublisher<ShieldLostMessage> _shieldLostPublisher;
        [Inject] private IPublisher<ProjectileFiredMessage> _firedPublisher;
        [Inject] private IPublisher<SpeckSpawnRequestMessage> _speckPublisher;
        [Inject] private ISubscriber<BalloonDeflectedMessage> _deflectedSubscriber;
        [Inject] private ProjectileHitResolver _hitResolver;
        [Inject] private ProjectileMotionResolver _motionResolver;
        [Inject] private PauseService _pauseService;
        [Inject] private DisturbanceFieldService _disturbanceField;
        [Inject] private SceneLightFieldService _lightField;

        private IWriteableProjectileModel _model;
        private Light _light;
        private IDisposable _lightRegistration;
        private int _sparksColorIndex = -1;
        private IDisposable _deflectedSubscription;
        private ProjectileTrail _projectileTrail;
        private bool _shieldShown;
        private ProjectileShieldView _shieldView;
        private Vector3 _baseScale;
        private bool _disappearing;
        private Color[] _paletteColors;
        private float _rainbowGlowTimer;
        private bool _rainbowGlowActive;
        private bool _hasFlown;

        /// <summary>True once the fired shot has taken at least one physics step.</summary>
        internal bool HasFlown => _hasFlown;

        private void Awake()
        {
            // NameToLayer cannot be called from a static field initializer on a MonoBehaviour.
            if (BalloonsLayer == -1)
            {
                BalloonsLayer = LayerMask.NameToLayer("Balloons");
            }

            _baseScale = transform.localScale;
            _shieldView = GetComponentInChildren<ProjectileShieldView>(true);
            _projectileTrail = GetComponentInChildren<ProjectileTrail>(true);
        }

        private void Update()
        {
            if (_model == null || _pauseService.IsAnyPaused.Value)
            {
                return;
            }

            // Null until the shot fires (registered in FixedUpdate's first free frame).
            if (_light != null)
            {
                _light.Position.Value = transform.position;
            }

            TickRainbowGlow();
        }

        private void FixedUpdate()
        {
            if (_disappearing || _model == null || !_model.IsFree || _pauseService.IsAnyPaused.Value)
            {
                return;
            }

            // The first free frame is the shot leaving the muzzle (MoveAndBounce sets _hasFlown below): emit the
            // exit-force burst once here, before the shot advances, and light the shot up — the light is a
            // fired-shot thing, not lit while it's still held at the thrower.
            if (!_hasFlown)
            {
                EmitFireBurst();

                // Colourless shots read as the Sparks tint; recoloured shots take their own colour
                // (kept in step by UpdateGlowColor).
                _light = new Light(transform.position, _lightRadius, _lightIntensity, LightColorIndex());
                _lightRegistration = _lightField.RegisterLight(_light);
            }

            RevealShieldOnFirstFreeFrame();
            MoveAndBounce();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_disappearing || _model == null || !_model.IsFree || _pauseService.IsAnyPaused.Value)
            {
                return;
            }

            if (!TryGetHitBalloon(other, out var balloonView, out var balloonModel))
            {
                return;
            }

            switch (_hitResolver.Resolve(_model, balloonModel, balloonView.transform.position))
            {
                case ProjectileHitVisual.Recolored:
                    UpdateGlowColor();
                    break;
                case ProjectileHitVisual.Destroyed:
                    DestroyProjectile();
                    break;
            }
        }

        // Clear the last-hit guard once the shot leaves that balloon's collider, so a fresh re-approach can hit
        // it again — a surviving tough the shot deflected off and later returns to would otherwise pass straight
        // through. The guard only exists to stop an immediate re-hit on the overlap a deflect leaves behind.
        private void OnTriggerExit2D(Collider2D other)
        {
            if (_model == null || other.gameObject.layer != BalloonsLayer)
            {
                return;
            }

            var balloonView = other.GetComponent<BalloonView>();
            if (balloonView != null && _model.LastHitBalloon == balloonView.Model)
            {
                _model.LastHitBalloon = null;
            }
        }

        public void OnSpawned()
        {
            _shieldShown = false;
            _disappearing = false;
            _hasFlown = false;
            _rainbowGlowActive = false;
            _rainbowGlowTimer = 0f;
            _deflectedSubscription?.Dispose();
            _deflectedSubscription = null;

            // Mirror OnDespawned's cleanup: a pooled instance must never inherit a still-running
            // disappear tween from its previous life — that would scale the fresh projectile to zero
            // (and fire its destroy callback) mid-flight, in open space.
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.rotation = Quaternion.identity;
            if (_glowRenderer != null)
            {
                _glowRenderer.DOKill();
                _glowRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            _projectileTrail?.Disable();
        }

        public void OnDespawned()
        {
            _lightRegistration?.Dispose();
            _lightRegistration = null;
            _light = null;
            _deflectedSubscription?.Dispose();
            _deflectedSubscription = null;
            _model = null;
            _shieldShown = false;
            _disappearing = false;
            _rainbowGlowActive = false;
            _rainbowGlowTimer = 0f;
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.rotation = Quaternion.identity;
            if (_glowRenderer != null)
            {
                _glowRenderer.DOKill();
                _glowRenderer.color = new Color(1f, 1f, 1f, 0f);
            }

            _projectileTrail?.Disable();
            if (_shieldView != null)
            {
                _shieldView.Reset();
            }
        }

        public void Bind(IWriteableProjectileModel model)
        {
            _model = model;
            _shieldShown = false;
            if (_shieldView != null)
            {
                _shieldView.Bind(model);
            }

            _deflectedSubscription?.Dispose();
            _deflectedSubscription = _deflectedSubscriber.Subscribe(OnBalloonDeflected);
        }

        private void DestroyProjectile()
        {
            // Publish now, not after the scale-down: the thrower scales this shot away (it returns to the
            // pool once that finishes) and loads a fresh instance, so it never reuses one mid-disappear.
            _balancePublisher.Publish(default);
            _destroyedPublisher.Publish(default);
        }

        /// <summary>Scales the projectile to zero then invokes <paramref name="onComplete" />; runs in unscaled time so it still plays while the world is frozen.</summary>
        internal void PlayDisappear(Action onComplete = null)
        {
            if (_disappearing)
            {
                return;
            }

            _disappearing = true;
            _projectileTrail?.Disable();

            transform.DOKill();

            var duration = _config.ProjectileDisappearDuration;

            // A dead shot keeps drifting along its heading (fired shots only) instead of freezing in place.
            if (_model != null && _model.IsFree && _config.ProjectileDeadDriftFactor > 0f)
            {
                Vector3 heading = _model.Direction;
                var target = transform.position + heading.normalized * (_model.Speed * duration * _config.ProjectileDeadDriftFactor);
                transform.DOMove(target, duration).SetUpdate(true);
            }

            transform.DOScale(Vector3.zero, duration)
                .SetEase(_config.ProjectileDisappearEase)
                .SetUpdate(true)
                .OnComplete(() => onComplete?.Invoke());
        }

        private void MoveAndBounce()
        {
            var step = _motionResolver.Step(_model, transform.position, Time.fixedDeltaTime);

            if (step.Outcome != ProjectileStepOutcome.Moved)
            {
                PlayBounceEffect(step.Position);
            }

            if (step.Outcome == ProjectileStepOutcome.Destroyed)
            {
                DestroyProjectile();
                return;
            }

            if (step.Outcome == ProjectileStepOutcome.Bounced)
            {
                _shieldLostPublisher.Publish(new ShieldLostMessage(step.Position));
            }

            transform.position = step.Position;
            transform.up = step.Direction;
            _hasFlown = true;

            _disturbanceField.Stamp(StampSource.Projectile, step.Position, step.Direction);
        }

        // The muzzle-exit force as a line of stamps marching along the fire heading — count = the ProjectileFire
        // profile's Interval (repurposed for this event-driven profile), spaced by its radius. Specks are seeded
        // along the same line FIRST, so the following stamps agitate them and the shot kicks dust up its exit path.
        // Publishes ProjectileFiredMessage so VFX/audio/camera can react to the exact fire moment.
        private void EmitFireBurst()
        {
            var profile = _disturbanceField.GetProfile(StampSource.ProjectileFire);
            var count = Mathf.Max(1, Mathf.RoundToInt(profile.Interval));
            var heading = _model.Direction;
            var spacing = profile.Spacing > 0f ? profile.Spacing : profile.Radius;
            var step = heading.normalized * spacing;

            // Seed specks along the exit line first, so the cone stamps then agitate them.
            for (var i = 0; i < count; i++)
            {
                _speckPublisher?.Publish(
                    new SpeckSpawnRequestMessage(SpeckSource.ProjectileFire, transform.position + step * i));
            }

            // The exit force itself: a cone marched along the heading (see DisturbanceFieldService.StampCone).
            _disturbanceField.StampCone(
                StampSource.ProjectileFire, transform.position, heading,
                _palette.PaletteIndexOf(GamePalette.ProjectileColorId));

            _firedPublisher.Publish(new ProjectileFiredMessage(transform.position, heading));
        }

        private void OnBalloonDeflected(BalloonDeflectedMessage msg)
        {
            if (_model == null || msg.Balloon != _model.LastHitBalloon)
            {
                return;
            }

            _motionResolver.Deflect(_model, transform.position, msg.BalloonWorldPosition);
        }

        private void PlayBounceEffect(Vector3 position)
        {
            if (_shieldView == null || string.IsNullOrEmpty(_model?.ColorName.Value))
            {
                return;
            }

            _shieldView.PlayBounceVfx(position, _palette.GetColor(_model.ColorName.Value));
        }

        private void RevealShieldOnFirstFreeFrame()
        {
            if (_shieldShown || _shieldView == null)
            {
                return;
            }

            _shieldView.Show();
            _shieldShown = true;
            _projectileTrail?.Enable();
        }

        private bool TryGetHitBalloon(
            Collider2D other,
            out BalloonView balloonView,
            out IBalloonModel balloonModel)
        {
            balloonView = null;
            balloonModel = null;

            if (other.gameObject.layer != BalloonsLayer)
            {
                return false;
            }

            balloonView = other.GetComponent<BalloonView>();

            if (balloonView == null)
            {
                return false;
            }

            balloonModel = balloonView.Model;
            if (balloonModel == null)
            {
                Debug.LogWarning(
                    $"ProjectileView.TryGetHitBalloon: BalloonView on " +
                    $"\"{balloonView.gameObject.name}\" has a null Model — possible pool recycle race.",
                    balloonView);
                return false;
            }

            return _model.LastHitBalloon != balloonModel;
        }

        // While the rainbow buff is active the glow drives itself through the palette; on the frame the
        // buff clears it hands control back to the stolen-colour tween.
        private void TickRainbowGlow()
        {
            var active = _model.HasBuff<RainbowShieldProjectileBuff>();
            if (!active)
            {
                if (_rainbowGlowActive)
                {
                    _rainbowGlowActive = false;
                    UpdateGlowColor();
                }

                return;
            }

            if (!_rainbowGlowActive)
            {
                _rainbowGlowActive = true;
                _rainbowGlowTimer = 0f;
                _paletteColors ??= _palette.ColorValues();
                _glowRenderer?.DOKill();
            }

            if (_glowRenderer == null || _paletteColors.Length == 0)
            {
                return;
            }

            _rainbowGlowTimer += Time.deltaTime;
            var t = Mathf.Repeat(_rainbowGlowTimer * _rainbowGlowSpeed, 1f);
            _glowRenderer.color = ColorCycle.Sample(_paletteColors, t).WithAlpha(_glowAlpha);
        }

        private void UpdateGlowColor()
        {
            // The scene light follows the shot's colour too (colourless → Sparks).
            if (_light != null)
            {
                _light.PaletteIndex.Value = LightColorIndex();
            }

            if (_glowRenderer == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_model.ColorName.Value))
            {
                // Washed back to colourless (e.g. by soap) — fade the glow out.
                _glowRenderer.DOColor(new Color(1f, 1f, 1f, 0f), _glowColorDuration);
                return;
            }

            var color = _palette.GetColor(_model.ColorName.Value);
            _glowRenderer.DOColor(color.WithAlpha(_glowAlpha), _glowColorDuration);
        }

        // The palette index for the shot's light: its current colour, or the Sparks tint when colourless.
        private int LightColorIndex()
        {
            if (_sparksColorIndex < 0)
            {
                _sparksColorIndex = _palette.PaletteIndexOf(GamePalette.SparksColorId);
            }

            var index = _palette.PaletteIndexOf(_model.ColorName.Value);
            return index >= 0 ? index : _sparksColorIndex;
        }
    }
}
