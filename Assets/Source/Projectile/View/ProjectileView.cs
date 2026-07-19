using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
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
using UniRx;
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

        [Header("Glow")] [SerializeField] private ColorableRenderer[] _glowRenderers;

        [SerializeField] [Range(0f, 1f)] private float _glowAlpha = 0.5f;
        [SerializeField] private float _glowColorDuration = 0.2f;
        [Tooltip("Full palette loops per second the glow cycles through while the rainbow buff is active.")]
        [SerializeField] [Min(0f)] private float _rainbowGlowSpeed = 1.5f;

        [Header("Pierce Spiral")]
        [Tooltip("Child renderer carrying the PierceConeSpiral material — fades in once the shot earns " +
                 "the piercing state (cruise taps), fades out with it or during the doomed last breath.")]
        [SerializeField] private SpriteRenderer _pierceSpiralRenderer;
        [Tooltip("Seconds for the spiral to lerp in/out when the piercing state flips.")]
        [SerializeField] [Min(0f)] private float _pierceFadeDuration = 0.35f;

        [Tooltip("How far the piercing aura DUCKS (not off) during each cruise tap beat. A piercing " +
                 "shot re-taps faster than the tap-ease window, so ducking fully to 0 would keep the " +
                 "aura permanently hidden exactly while it's armed.")]
        [SerializeField] [Range(0f, 1f)] private float _pierceTapBeatAlpha = 0.5f;

        [Header("Scene Light")]
        [Tooltip("Radius of the light this shot casts into the scene-light field. Keep small — it's a bullet.")]
        [SerializeField] [Min(0f)] private float _lightRadius = 0.6f;
        [SerializeField] [Min(0f)] private float _lightIntensity = 1.5f;

        [Header("Shield-Loss Flash")]
        [Tooltip("A brief Sparks-colour light popped at the wall each time a bounce spends a shield.")]
        [SerializeField] [Min(0f)] private float _shieldFlashIntensity = 2f;
        [SerializeField] [Min(0f)] private float _shieldFlashRadius = 0.9f;
        [SerializeField] [Min(0f)] private float _shieldFlashDuration = 0.12f;

        [Inject] private IGameConfiguration _config;
        [Inject] private IGamePalette _palette;
        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private IPublisher<ShieldLostMessage> _shieldLostPublisher;
        [Inject] private IPublisher<ProjectileFiredMessage> _firedPublisher;
        [Inject] private IPublisher<ProjectileCruiseStartedMessage> _cruiseStartedPublisher;
        [Inject] private IPublisher<ProjectileCruiseEndedMessage> _cruiseEndedPublisher;
        [Inject] private IPublisher<ProjectileDoomedStartedMessage> _doomedStartedPublisher;
        [Inject] private IPublisher<ProjectileDoomedEndedMessage> _doomedEndedPublisher;
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
        private Light _shieldFlashLight;
        private IDisposable _shieldFlashRegistration;
        private float _shieldFlashOffTime;
        private int _sparksColorIndex = -1;
        private IDisposable _deflectedSubscription;
        private IDisposable _cruiseSubscription;
        private IDisposable _doomedSubscription;
        private ProjectileTrail _projectileTrail;
        private float _contactRadius;
        private bool _shieldShown;
        private ProjectileShieldView _shieldView;
        private Vector3 _baseScale;
        private bool _disappearing;
        private Color[] _paletteColors;
        private Color _glowColor;
        private Tween _glowTween;
        private float _rainbowGlowTimer;
        private bool _rainbowGlowActive;
        private bool _hasFlown;
        private float _pierceAlpha;
        private PathTrace.SegmentBlocked _segmentBlocked;

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

            // World contact radius for exact-contact deflection: the tightest half-extent of our own
            // collider (a capsule's cross-section radius) — cached once, colliders don't change.
            var collider = GetComponent<Collider2D>();
            _contactRadius = collider is CircleCollider2D circle
                ? circle.radius * transform.lossyScale.x
                : collider is CapsuleCollider2D capsule
                    ? Mathf.Min(capsule.size.x, capsule.size.y) * 0.5f * transform.lossyScale.x
                    : 0f;
            _shieldView = GetComponentInChildren<ProjectileShieldView>(true);
            _projectileTrail = GetComponentInChildren<ProjectileTrail>(true);
        }

        private void Update()
        {
            if (_model == null || _pauseService.IsAnyPaused.Value)
            {
                return;
            }

            // Null until the shot fires (registered in FixedUpdate's first free frame). Track both ends to
            // the same point so it stays a point light as it moves — otherwise the segment would stretch
            // from the muzzle (the fixed EndPosition) to the current position.
            if (_light != null)
            {
                _light.Position.Value = transform.position;
                _light.EndPosition.Value = transform.position;
            }

            TickRainbowGlow();
            TickPierceSpiral();

            if (_shieldFlashRegistration != null && Time.time >= _shieldFlashOffTime)
            {
                EndShieldFlash();
            }
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

                // The muzzle is the first flight segment's origin — the last-shield ease measures
                // approach progress from wherever the current segment began (updated on each bounce).
                _model.Flight.SegmentStartPosition = transform.position;

                // Colourless shots read as the Sparks tint; recoloured shots take their own colour
                // (kept in step by UpdateGlowColor).
                _light = new Light(transform.position, _lightRadius, _lightIntensity, LightColorIndex());
                _lightRegistration = _lightField.RegisterLight(_light);

                // Fade the glow in on fire, even before a colour hit — colourless shots use the
                // Sparks palette tint so the glow is always visible while the shot is in flight.
                ActivateInitialGlow();
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
            ResetPierceSpiral();
            EndShieldFlash();
            LifecycleHelper.DisposeAndClear(ref _deflectedSubscription);
            LifecycleHelper.DisposeAndClear(ref _cruiseSubscription);
            LifecycleHelper.DisposeAndClear(ref _doomedSubscription);

            // Mirror OnDespawned's cleanup: a pooled instance must never inherit a still-running
            // disappear tween from its previous life — that would scale the fresh projectile to zero
            // (and fire its destroy callback) mid-flight, in open space.
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.rotation = Quaternion.identity;
            KillGlowTween();
            ApplyGlow(new Color(1f, 1f, 1f, 0f));

            _projectileTrail?.Disable();
        }

        public void OnDespawned()
        {
            LifecycleHelper.DisposeAndClear(ref _lightRegistration);
            _light = null;
            EndShieldFlash();
            LifecycleHelper.DisposeAndClear(ref _deflectedSubscription);
            LifecycleHelper.DisposeAndClear(ref _cruiseSubscription);
            LifecycleHelper.DisposeAndClear(ref _doomedSubscription);
            _model = null;
            _shieldShown = false;
            _disappearing = false;
            _rainbowGlowActive = false;
            _rainbowGlowTimer = 0f;
            ResetPierceSpiral();
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.rotation = Quaternion.identity;
            KillGlowTween();
            ApplyGlow(new Color(1f, 1f, 1f, 0f));

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

            // A fresh model always starts un-cruising, so skip the initial value — only transitions publish.
            _cruiseSubscription?.Dispose();
            _cruiseSubscription = model.IsCruising
                .SkipLatestValueOnSubscribe()
                .Subscribe(OnCruiseChanged);

            _doomedSubscription?.Dispose();
            _doomedSubscription = model.IsLastShieldApproach
                .SkipLatestValueOnSubscribe()
                .Subscribe(OnDoomedChanged);
        }

        private void DestroyProjectile()
        {
            // Shatter any toughs the shot plowed through but never discharged — a piercing run that
            // ends (out of shields / despawn) with pending toughs flushes them here so none are dropped,
            // with the same trail flourish the mid-flight discharge gets.
            if (_model != null && _model.Flight.PendingPierceHits.Count > 0)
            {
                _hitResolver.DischargePending(_model);
                _projectileTrail?.Boost();
            }

            // A shot that dies mid-cruise (last shield spent on a wall) still closes its cruise, so
            // started/ended feedback always pairs up.
            if (_model != null && _model.IsCruising.Value)
            {
                _model.IsCruising.Value = false;
            }

            // Critical: the doomed shot dies WITH IsLastShieldApproach still true (the final step set
            // it, then the wall killed it). Close it here so the doomed-ended message fires — else the
            // slow-mo time-scale claim never releases and the spawner stays paused forever.
            if (_model != null && _model.IsLastShieldApproach.Value)
            {
                _model.IsLastShieldApproach.Value = false;
            }

            // A dead shot isn't piercing. Clear it too, else the aura's doom guard (which keyed off
            // IsLastShieldApproach) is defeated the instant the line above runs — pierceActive turns
            // true again and the spiral flashes on over the disappear, right at the death wall.
            if (_model != null && _model.IsPiercing.Value)
            {
                _model.IsPiercing.Value = false;
            }

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
            // A shot with no shields left dies at the next wall — UNLESS a balloon in the way could
            // still pop and refund one. When the single segment ahead is clear of any balloon, that
            // wall is certain death: flag the doomed run so the resolver eases the last-breath drift.
            var wasDoomed = _model.IsLastShieldApproach.Value;
            var doomed = _model.ShieldsRemaining.Value == 0
                && IsPathClearAhead(transform.position, _model.Direction, 1);
            _model.IsLastShieldApproach.Value = doomed;

            // Anchor the last-breath glide at the instant doom begins. The resolver eases position from
            // SegmentStartPosition over SegmentElapsed/duration, but those track the last WALL bounce —
            // a shot only reaches 0 shields there, yet stays UN-doomed while a balloon blocks the path
            // (it could still pop and refund). Once that balloon clears, doom flips on mid-segment with
            // SegmentElapsed already past duration, so the first doomed step overshoots the death wall
            // and returns Destroyed before transform.position is written — the shot vanishes in midair.
            // Re-anchoring on the rising edge gives the glide its full timed runway from here to the wall.
            if (doomed && !wasDoomed)
            {
                _model.Flight.SegmentStartPosition = transform.position;
                _model.Flight.SegmentElapsed = 0f;
            }

            var step = _motionResolver.Step(_model, transform.position, Time.fixedDeltaTime);

            if (step.Outcome != ProjectileStepOutcome.Moved)
            {
                PlayBounceEffect(step.WallContact);
            }

            if (step.Outcome == ProjectileStepOutcome.Destroyed)
            {
                _disturbanceField.Stamp(StampSource.ProjectileImpact, step.WallContact, Vector2.zero);
                FlashShieldLoss(step.WallContact);
                DestroyProjectile();
                return;
            }

            if (step.Outcome == ProjectileStepOutcome.Bounced)
            {
                _shieldLostPublisher.Publish(new ShieldLostMessage(step.WallContact));
                TryEnterCruise(step.Position, step.Direction);

                // Punctuate the bounce: a radial impact into the motion field at the wall, plus a
                // brief Sparks-colour light flash at the same point.
                _disturbanceField.Stamp(StampSource.ProjectileImpact, step.WallContact, Vector2.zero);
                FlashShieldLoss(step.WallContact);
            }

            // The motion resolver ends the pierce when the discharge countdown elapses; the plowed toughs
            // are still pending, so shatter them now (at their strike positions). DestroyProjectile handles
            // the same flush on a shot that dies before the countdown fires.
            if (!_model.IsPiercing.Value && _model.Flight.PendingPierceHits.Count > 0)
            {
                _hitResolver.DischargePending(_model);
                _projectileTrail?.Boost();
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

            var contact = _motionResolver.Deflect(
                _model, transform.position, msg.BalloonWorldPosition, msg.SurfaceRadius + _contactRadius);
            transform.position = contact;

            // The same radial impact as a wall bounce, at the deflect point.
            _disturbanceField.Stamp(StampSource.ProjectileImpact, contact, Vector2.zero);
        }

        // The resolver counts empty bounces; entry is confirmed HERE because it needs physics: past
        // the threshold, trace the wall-reflected ray ahead and only an actually-empty corridor —
        // no balloon within the next threshold bounces — earns the speed ramp. Re-checked every
        // bounce, so a corridor that opens up mid-flight can still trigger it.
        private void TryEnterCruise(Vector3 position, Vector3 direction)
        {
            var threshold = _config.CruiseWallBounceThreshold;
            // A shot already piercing without cruising is a Snipe lance — it must not enter cruise, which
            // would layer on the per-shield speed tap it deliberately excludes. (A cruise-earned pierce is
            // always already cruising, so this only gates the Snipe case.)
            if (threshold <= 0 || _model.IsCruising.Value || _model.IsPiercing.Value
                || _model.Flight.ConsecutiveWallBounces < threshold)
            {
                return;
            }

            if (!IsPathClearAhead(position, direction, threshold))
            {
                return;
            }

            _model.Flight.CruiseStartShields = _model.ShieldsRemaining.Value;
            _model.Flight.CruiseTapElapsed = 0f;
            _model.IsCruising.Value = true;
        }

        private bool IsPathClearAhead(Vector3 position, Vector3 direction, int bounces)
        {
            // Cached once (this runs every fixed step) so the closure over the collider query isn't
            // re-allocated per call. The solver mirrors this trace analytically — see PathTrace.
            _segmentBlocked ??= (from, dir, length) =>
                Physics2D.CircleCast(from, _contactRadius, dir, length, 1 << BalloonsLayer).collider != null;

            return PathTrace.IsClearAhead(_motionResolver.Walls, position, direction, bounces, _segmentBlocked);
        }

        // The earned-piercing aura eases in rather than popping on — the buff lands mid-flight at a
        // wall bounce, and an instant full-strength spiral there reads as a glitch, not a power-up.
        // While armed it stays lit; each tap beat (the freeze-then-pickup window every cruise bounce
        // replays) only DUCKS it toward _pierceTapBeatAlpha and it re-flourishes after — the aura
        // winding back up with the shot.
        private void TickPierceSpiral()
        {
            if (_pierceSpiralRenderer == null)
            {
                return;
            }

            // A piercing shot re-taps faster than the tap-ease window, so ducking to 0 here would keep
            // the aura hidden the whole time it's armed — dim toward the floor instead. Still hidden
            // entirely while doomed (drifting to its death): a flourish there reads as a power-up right
            // as it dies, and the clear path means there's nothing to pierce anyway.
            var inTapBeat = _model.IsCruising.Value
                            && _model.Flight.CruiseTapElapsed < _config.CruiseTapEaseDuration;
            var pierceActive = _model.IsPiercing.Value && !_model.IsLastShieldApproach.Value;
            var target = pierceActive
                ? (inTapBeat ? _pierceTapBeatAlpha : 1f)
                : 0f;
            var maxStep = _pierceFadeDuration > 0f ? Time.deltaTime / _pierceFadeDuration : 1f;
            _pierceAlpha = Mathf.MoveTowards(_pierceAlpha, target, maxStep);

            _pierceSpiralRenderer.enabled = _pierceAlpha > 0f;
            var color = _pierceSpiralRenderer.color;
            color.a = _pierceAlpha;
            _pierceSpiralRenderer.color = color;
        }

        private void ResetPierceSpiral()
        {
            _pierceAlpha = 0f;
            if (_pierceSpiralRenderer == null)
            {
                return;
            }

            _pierceSpiralRenderer.enabled = false;
            var color = _pierceSpiralRenderer.color;
            color.a = 0f;
            _pierceSpiralRenderer.color = color;
        }

        private void OnCruiseChanged(bool isCruising)
        {
            if (isCruising)
            {
                _cruiseStartedPublisher.Publish(new ProjectileCruiseStartedMessage(
                    transform.position, _model.Direction, _model.ShieldsRemaining.Value));
            }
            else
            {
                _cruiseEndedPublisher.Publish(new ProjectileCruiseEndedMessage(transform.position));
            }
        }

        // A brief Sparks-colour light at the wall where a bounce just spent a shield — fixed at the
        // contact point (it doesn't follow the shot on) and snapped off after the flash duration.
        private void FlashShieldLoss(Vector3 position)
        {
            if (_sparksColorIndex < 0)
            {
                _sparksColorIndex = _palette.PaletteIndexOf(GamePalette.SparksColorId);
            }

            _shieldFlashLight ??= new Light(position, _shieldFlashRadius, _shieldFlashIntensity, _sparksColorIndex);
            _shieldFlashLight.Position.Value = position;
            _shieldFlashLight.EndPosition.Value = position;
            _shieldFlashLight.Radius.Value = _shieldFlashRadius;
            _shieldFlashLight.EndRadius.Value = _shieldFlashRadius;
            _shieldFlashLight.Intensity.Value = _shieldFlashIntensity;
            _shieldFlashLight.PaletteIndex.Value = _sparksColorIndex;
            _shieldFlashRegistration ??= _lightField.RegisterLight(_shieldFlashLight);
            _shieldFlashOffTime = Time.time + _shieldFlashDuration;
        }

        private void EndShieldFlash()
        {
            LifecycleHelper.DisposeAndClear(ref _shieldFlashRegistration);
        }

        private void OnDoomedChanged(bool doomed)
        {
            if (doomed)
            {
                _doomedStartedPublisher.Publish(new ProjectileDoomedStartedMessage(transform.position));
            }
            else
            {
                _doomedEndedPublisher.Publish(new ProjectileDoomedEndedMessage());
            }
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
            var active = _model.HasBuff(ProjectileBuffId.RainbowShield);
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
                KillGlowTween();
            }

            if (_paletteColors.Length == 0)
            {
                return;
            }

            _rainbowGlowTimer += Time.deltaTime;
            var t = Mathf.Repeat(_rainbowGlowTimer * _rainbowGlowSpeed, 1f);
            ApplyGlow(ColorCycle.Sample(_paletteColors, t).WithAlpha(_glowAlpha));
        }

        private void ActivateInitialGlow()
        {
            var target = string.IsNullOrEmpty(_model.ColorName.Value)
                ? _palette.GetColor(GamePalette.SparksColorId).WithAlpha(_glowAlpha)
                : _palette.GetColor(_model.ColorName.Value).WithAlpha(_glowAlpha);
            TweenGlow(target);
        }

        private void UpdateGlowColor()
        {
            // The scene light follows the shot's colour too (colourless → Sparks).
            if (_light != null)
            {
                _light.PaletteIndex.Value = LightColorIndex();
            }

            // Colourless shots fall back to the Sparks palette tint so the glow stays visible.
            var target = string.IsNullOrEmpty(_model.ColorName.Value)
                ? _palette.GetColor(GamePalette.SparksColorId).WithAlpha(_glowAlpha)
                : _palette.GetColor(_model.ColorName.Value).WithAlpha(_glowAlpha);
            TweenGlow(target);
        }

        // Drives the glow renderers off a single tweened Color, so the smooth crossfades work across a
        // collection of ColorableRenderers (which expose SetColor, not a DOTween-able colour property).
        private void ApplyGlow(Color color)
        {
            _glowColor = color;
            _glowRenderers.SetColor(color);
        }

        private void TweenGlow(Color target)
        {
            KillGlowTween();
            _glowTween = DOTween.To(() => _glowColor, ApplyGlow, target, _glowColorDuration);
        }

        private void KillGlowTween()
        {
            LifecycleHelper.KillAndClear(ref _glowTween);
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
