using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Diagnostics;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Rendering;
using BalloonParty.Shared.SceneLight;
using BalloonParty.Shared.Messages;
using BalloonParty.Scenario;
using BalloonParty.Slots.Actor;
using DG.Tweening;
using MessagePipe;
using UniRx;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Light = BalloonParty.Shared.SceneLight.Light;

namespace BalloonParty.Projectile.View
{
    public class ProjectileView : MonoBehaviour, IPoolable
    {
        private static int BalloonsLayer = -1;

        [Header("Glow")] [SerializeField] private ColorableRenderer[] _glowRenderers;

        [Header("Pierce Spiral")]
        [Tooltip("Child renderer carrying the PierceConeSpiral material — fades in once the shot earns " +
                 "the piercing state (cruise taps), fades out with it or during the doomed last breath.")]
        [SerializeField] private SpriteRenderer _pierceSpiralRenderer;

        [Inject] private IProjectileVisualConfig _visual;
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
        [Inject] private PaintingFieldService _paintingField;
        [Inject] private SceneLightFieldService _lightField;
        [Inject] private ISceneLightSettings _sceneLightSettings;

        private IWriteableProjectileModel _model;
        private Light _light;
        private IDisposable _lightRegistration;
        private Light _shieldFlashLight;
        private IDisposable _shieldFlashRegistration;
        private float _shieldFlashOffTime;
        private Light _sparkFlashLight;
        private IDisposable _sparkFlashRegistration;
        private float _sparkFlashOffTime;
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
        private Vector3 _lastPaintPos;

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
                if (_model.IsPiercing.Value && TryFindToughAhead(out var toughAhead))
                {
                    // Telegraph: stretch the shot's light into an area line reaching the tough it's about
                    // to punch through, so the armored contact reads a beat before it happens.
                    _light.EndPosition.Value = toughAhead;
                    _light.Radius.Value = _visual.PierceTelegraphHalfWidth;
                    _light.EndRadius.Value = _visual.PierceTelegraphHalfWidth;
                    _light.Intensity.Value = _visual.PierceTelegraphIntensity;
                }
                else
                {
                    // Back to a point light as it moves — else the segment would stretch from a stale end.
                    _light.EndPosition.Value = transform.position;
                    _light.Radius.Value = _visual.LightRadius;
                    _light.EndRadius.Value = _visual.LightRadius;
                    _light.Intensity.Value = _visual.LightIntensity;
                }
            }

            TickRainbowGlow();
            TickPierceSpiral();

            if (_shieldFlashRegistration != null && Time.time >= _shieldFlashOffTime)
            {
                EndShieldFlash();
            }

            if (_sparkFlashRegistration != null && Time.time >= _sparkFlashOffTime)
            {
                EndSparkFlash();
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
                _model.Flight.LastBouncePosition = transform.position;
                _model.Flight.SegmentSweepValid = true;

                // Colourless shots read as the Sparks tint; recoloured shots take their own colour
                // (kept in step by UpdateGlowColor).
                _light = new Light(transform.position, _visual.LightRadius, _visual.LightIntensity, LightColorIndex());
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

            // A plow records into PendingPierceHits without popping; if this contact did, spark it here at
            // the strike — synchronous, so every tough in a tight run flashes (a per-Update poll would
            // coalesce several substep plows into one).
            var pendingBefore = _model.Flight.PendingPierceHits.Count;

            switch (_hitResolver.Resolve(_model, balloonModel, balloonView.transform.position))
            {
                case ProjectileHitVisual.Recolored:
                    UpdateGlowColor();
                    break;
                case ProjectileHitVisual.Destroyed:
                    DestroyProjectile();
                    break;
            }

            var pending = _model.Flight.PendingPierceHits;
            if (pending.Count > pendingBefore)
            {
                FlashPierceSpark(pending[pending.Count - 1].Position);
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
            _lastPaintPos = Vector3.zero;
            _rainbowGlowActive = false;
            _rainbowGlowTimer = 0f;
            ResetPierceSpiral();
            EndShieldFlash();
            EndSparkFlash();
            LifecycleHelper.DisposeAndClear(ref _deflectedSubscription);
            LifecycleHelper.DisposeAndClear(ref _cruiseSubscription);
            LifecycleHelper.DisposeAndClear(ref _doomedSubscription);

#if UNITY_EDITOR
            ResetSweepGizmo();
#endif

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
            EndSparkFlash();
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

#if UNITY_EDITOR
            ResetSweepGizmo();
#endif

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

            var travelDirection = _model.Direction;
            var wasPiercing = _model.IsPiercing.Value;
            var segmentOrigin = (Vector3)_model.Flight.SegmentStartPosition;
            var step = _motionResolver.Step(_model, transform.position, Time.fixedDeltaTime);

            if (step.Outcome != ProjectileStepOutcome.Moved)
            {
                PlayBounceEffect(step.WallContact);
            }

            if (step.Outcome == ProjectileStepOutcome.Destroyed)
            {
                var velocityT = ComputeVelocityT(step.Speed);
                _disturbanceField.StampShieldLoss(StampSource.ProjectileImpact, step.WallContact, velocityT);
                FlashShieldLoss(step.WallContact, velocityT);
                DestroyProjectile();
                return;
            }

            if (step.Outcome == ProjectileStepOutcome.Bounced)
            {
                _shieldView?.OnBounce((Vector2)travelDirection, (Vector2)step.Direction, step.Speed);
                _shieldLostPublisher.Publish(new ShieldLostMessage(step.WallContact));
                TryAwardSweepTap(step.WallContact, travelDirection);

#if UNITY_EDITOR
                RecordSweepGizmoBounce(step.WallContact);
#endif

                _model.Flight.SegmentPopCount = 0;
                _model.Flight.SegmentSweepValid = true;
                _model.Flight.LastBouncePosition = step.WallContact;
                TryEnterCruise(step.Position, step.Direction);

                // Punctuate the bounce: a radial impact into the motion field at the wall, plus a
                // brief Sparks-colour light flash at the same point — both scaled by the shot's
                // velocity at this exact shield-loss instant (see ComputeVelocityT).
                var velocityT = ComputeVelocityT(step.Speed);
                _disturbanceField.StampShieldLoss(StampSource.ProjectileImpact, step.WallContact, velocityT);
                FlashShieldLoss(step.WallContact, velocityT);

                // The cruise-triggered speck burst: one request per bounce while cruising (including the
                // bounce that just started it), its count scaled by the shot's normalized velocity via
                // the profile's curve in SpeckField.
                if (_model.IsCruising.Value)
                {
                    _speckPublisher.Publish(new SpeckSpawnRequestMessage(
                        SpeckSource.ProjectileCruise, step.WallContact, velocityT));
                }
            }

            // Tunneling safety net: at any wall bounce while piercing, sweep the segment with a
            // CircleCastAll to catch toughs that OnTriggerEnter2D missed at high speed. Two cases:
            // 1. Resolver already ended pierce (pending > 0 detected) — we add any extras before discharge.
            // 2. ALL toughs were tunneled (pending was 0 at Step) — sweep finds them, then we end pierce.
            if (wasPiercing && step.Outcome == ProjectileStepOutcome.Bounced)
            {
                SweepPierceMisses(segmentOrigin, step.WallContact);

                // Case 2: the resolver left pierce active because it saw no pending hits, but our sweep
                // just found toughs — end the pierce now so the discharge below fires.
                if (_model.IsPiercing.Value && _model.Flight.PendingPierceHits.Count > 0)
                {
                    _model.EndPierce();
                }
            }

            // The motion resolver ends the pierce at a wall bounce when pending toughs exist; the plowed
            // toughs are still pending, so shatter them now (at their strike positions). DestroyProjectile
            // handles the same flush on a shot that dies before the next wall.
            if (!_model.IsPiercing.Value && _model.Flight.PendingPierceHits.Count > 0)
            {
                _hitResolver.DischargePending(_model);
                _projectileTrail?.Boost();
            }

            transform.position = step.Position;
            transform.up = step.Direction;
            _hasFlown = true;

            _disturbanceField.Stamp(StampSource.Projectile, step.Position, step.Direction);

            {
                var colorName = _model.ColorName.Value;
                var paletteIdx = !string.IsNullOrEmpty(colorName)
                    ? _palette.PaletteIndexOf(colorName)
                    : _palette.PaletteIndexOf(GamePalette.ProjectileColorId);
                var pos = step.Position;
                var prevPos = _lastPaintPos != Vector3.zero ? _lastPaintPos : pos;

                _paintingField.Paint(PaintSource.ProjectileTrail, pos, prevPos, paletteIdx);
                _paintingField.SetWindDampen(1f - ComputeVelocityT(step.Speed) * 0.7f);
                _lastPaintPos = pos;
            }
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

            var preDir = (Vector2)_model.Direction;
            var speed = _model.Speed;
            var contact = _motionResolver.Deflect(
                _model, transform.position, msg.BalloonWorldPosition, msg.SurfaceRadius + _contactRadius);
            transform.position = contact;
            _shieldView?.OnBounce(preDir, (Vector2)_model.Direction, speed);

            // Deflection interrupts free roaming — reset all progress toward piercing
            _model.Flight.SegmentPopCount = 0;
            _model.Flight.SegmentSweepValid = true;
            _model.Flight.LastBouncePosition = contact;
            _model.Flight.ConsecutiveWallBounces = 0;
            _model.Flight.TotalSweeps = 0;
            _model.Flight.TotalCruiseTaps = 0;

            if (_model.IsCruising.Value)
            {
                _model.IsCruising.Value = false;
            }

#if UNITY_EDITOR
            ResetSweepGizmo();
#endif

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

            _model.Flight.CruiseTapElapsed = 0f;
            _model.IsCruising.Value = true;
        }

        private void TryAwardSweepTap(Vector3 wallHitPosition, Vector3 travelDirection)
        {
            if (!_config.SweepEnabled || _model.Flight.SegmentPopCount <= 0 || !_model.Flight.SegmentSweepValid)
            {
                return;
            }

            var segmentLength = Vector3.Distance(_model.Flight.LastBouncePosition, wallHitPosition);
            if (segmentLength <= 0f || travelDirection.sqrMagnitude < 1e-6f)
            {
                return;
            }

            var backward = -((Vector2)travelDirection).normalized;
            var hit = Physics2D.CircleCast(wallHitPosition, _contactRadius, backward, segmentLength, 1 << BalloonsLayer);
            if (hit.collider != null)
            {
                return;
            }

            _model.Flight.TotalSweeps++;

#if UNITY_EDITOR
            StartSweepGizmoTracking();
#endif

            if (_config.SweepTapThreshold > 0 && _model.Flight.TotalSweeps < _config.SweepTapThreshold)
            {
                return;
            }

#if UNITY_EDITOR
            _sweepGizmoThresholdReached = true;
            if (_sweepGizmoWarmupPath.Count > 0)
            {
                _sweepGizmoPostPath.Add(_sweepGizmoWarmupPath[_sweepGizmoWarmupPath.Count - 1]);
            }
#endif

            _model.Flight.TotalCruiseTaps++;
            _model.Flight.CruiseTapElapsed = 0f;

            var threshold = _config.CruisePiercingTapThreshold;
            if (threshold > 0 && !_model.IsPiercing.Value && _model.Flight.TotalCruiseTaps >= threshold)
            {
                _model.IsPiercing.Value = true;
            }
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
        // replays) only DUCKS it toward PierceTapBeatAlpha and it re-flourishes after — the aura
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
            var inTapBeat = (_model.IsCruising.Value || _model.Flight.TotalCruiseTaps > 0)
                            && _model.Flight.CruiseTapElapsed < _config.CruiseTapEaseDuration;
            var pierceActive = _model.IsPiercing.Value && !_model.IsLastShieldApproach.Value;
            var target = pierceActive
                ? (inTapBeat ? _visual.PierceTapBeatAlpha : 1f)
                : 0f;

            if (pierceActive && _pierceAlpha < target && TryFindToughAhead(out var toughPos))
            {
                // Distance-based fade-in: alpha tracks spatial progress toward the tough ahead.
                // PierceFadeInReach shrinks the effective distance so full alpha arrives before
                // impact, and PierceFadeInPower (< 1) front-loads the curve so early travel
                // already shows significant aura. Never decreases alpha.
                var segStart = (Vector3)_model.Flight.SegmentStartPosition;
                var totalDist = Vector3.Distance(segStart, toughPos) * _visual.PierceFadeInReach;
                var traveled = Vector3.Distance(segStart, transform.position);
                var linear = totalDist > 0f ? Mathf.Clamp01(traveled / totalDist) : 1f;
                var progress = Mathf.Pow(linear, _visual.PierceFadeInPower);
                _pierceAlpha = Mathf.Max(_pierceAlpha, Mathf.Min(progress, target));
            }
            else
            {
                // No tough in sight, already at target, tap-beat duck, or fading out: lerp with
                // unscaled time so slow-mo doesn't stall it.
                var maxStep = _visual.PierceFadeDuration > 0f
                    ? Time.unscaledDeltaTime / _visual.PierceFadeDuration
                    : 1f;
                _pierceAlpha = Mathf.MoveTowards(_pierceAlpha, target, maxStep);
            }

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

        // Normalizes a resolved flight speed into [0,1] for the shield-loss stamp curves: t=0 at the
        // shot's base (un-buffed, non-cruising) speed, t=1 at the fastest the cruise ramp can reach. A
        // stacked buff (e.g. Snipe) that pushes speed past that ceiling just saturates at 1 rather than
        // breaking the curve lookup.
        private float ComputeVelocityT(float speed)
        {
            var normalSpeed = _config.ProjectileSpeed;
            var maxSpeed = normalSpeed * _config.MaxCruiseSpeedMultiplier;
            return maxSpeed > normalSpeed ? Mathf.Clamp01((speed - normalSpeed) / (maxSpeed - normalSpeed)) : 0f;
        }

        // A brief Sparks-colour light at the wall where a bounce just spent a shield — fixed at the
        // contact point (it doesn't follow the shot on) and snapped off after the flash duration.
        // Radius/intensity scale by the shot's velocity at the bounce (see ComputeVelocityT).
        private void FlashShieldLoss(Vector3 position, float velocityT)
        {
            if (_sparksColorIndex < 0)
            {
                _sparksColorIndex = _palette.PaletteIndexOf(GamePalette.SparksColorId);
            }

            var curve = _sceneLightSettings.ShieldLossLightVelocityCurve;
            var radius = curve.ScaleByVelocity(_visual.ShieldFlashRadius, velocityT);
            var intensity = curve.ScaleByVelocity(_visual.ShieldFlashIntensity, velocityT);

            _shieldFlashLight ??= new Light(position, radius, intensity, _sparksColorIndex);
            _shieldFlashLight.Position.Value = position;
            _shieldFlashLight.EndPosition.Value = position;
            _shieldFlashLight.Radius.Value = radius;
            _shieldFlashLight.EndRadius.Value = radius;
            _shieldFlashLight.Intensity.Value = intensity;
            _shieldFlashLight.PaletteIndex.Value = _sparksColorIndex;
            _shieldFlashRegistration ??= _lightField.RegisterLight(_shieldFlashLight);
            _shieldFlashOffTime = Time.time + _visual.ShieldFlashDuration;
        }

        private void EndShieldFlash()
        {
            LifecycleHelper.DisposeAndClear(ref _shieldFlashRegistration);
        }

        // Pop a brief Sparks-colour flash at a tough the piercing shot just plowed. Called synchronously
        // from OnTriggerEnter2D per plow, at the strike point.
        private void FlashPierceSpark(Vector3 position)
        {
            if (_sparksColorIndex < 0)
            {
                _sparksColorIndex = _palette.PaletteIndexOf(GamePalette.SparksColorId);
            }

            _sparkFlashLight ??= new Light(position, _visual.PierceSparkRadius, _visual.PierceSparkIntensity, _sparksColorIndex);
            _sparkFlashLight.Position.Value = position;
            _sparkFlashLight.EndPosition.Value = position;
            _sparkFlashLight.Radius.Value = _visual.PierceSparkRadius;
            _sparkFlashLight.EndRadius.Value = _visual.PierceSparkRadius;
            _sparkFlashLight.Intensity.Value = _visual.PierceSparkIntensity;
            _sparkFlashLight.PaletteIndex.Value = _sparksColorIndex;
            _sparkFlashRegistration ??= _lightField.RegisterLight(_sparkFlashLight);
            _sparkFlashOffTime = Time.time + _visual.PierceSparkDuration;
        }

        private void EndSparkFlash()
        {
            LifecycleHelper.DisposeAndClear(ref _sparkFlashRegistration);
        }

        // The next tough (hits>1) on the current flight segment, if any — a bounded forward cast up to the
        // wall the shot is heading for. Drives the telegraph. TryGetHitBalloon skips the just-plowed
        // balloon (LastHitBalloon), so the telegraph reaches the NEXT tough, not the one behind the shot.
        private bool TryFindToughAhead(out Vector3 position)
        {
            position = default;
            var origin = transform.position;
            if (!_motionResolver.Walls.TryFindCrossing(origin, _model.Direction, out var crossing, out _))
            {
                return false;
            }

            var direction = ((Vector2)_model.Direction).normalized;
            var length = Vector2.Distance(origin, crossing);
            var hit = Physics2D.CircleCast(origin, _contactRadius, direction, length, 1 << BalloonsLayer);
            if (hit.collider == null || !TryGetHitBalloon(hit.collider, out _, out var balloonModel))
            {
                return false;
            }

            // Only telegraph a tough the shot hasn't already plowed this run — else a just-plowed tough
            // (or an earlier one still on the board after a bounce) would re-light a stale line.
            if (balloonModel.IsTough() && !AlreadyPlowed(balloonModel))
            {
                position = hit.collider.transform.position;
                return true;
            }

            return false;
        }

        private bool AlreadyPlowed(IBalloonModel balloon)
        {
            var pending = _model.Flight.PendingPierceHits;
            for (var i = 0; i < pending.Count; i++)
            {
                if (ReferenceEquals(pending[i].Balloon, balloon))
                {
                    return true;
                }
            }

            return false;
        }

        // Tunneling safety net: a full-segment CircleCastAll from the previous wall to the current
        // wall, catching any tough balloons OnTriggerEnter2D missed at high speed. Adds them to
        // PendingPierceHits so the discharge that follows includes every tough in the path.
        private void SweepPierceMisses(Vector3 segmentStart, Vector3 wallHit)
        {
            var direction = (Vector2)(wallHit - segmentStart);
            var length = direction.magnitude;
            if (length < 1e-4f)
            {
                return;
            }

            direction /= length;
            var hits = Physics2D.CircleCastAll(segmentStart, _contactRadius, direction, length, 1 << BalloonsLayer);
            if (hits.Length == 0)
            {
                return;
            }

            var pending = _model.Flight.PendingPierceHits;
            foreach (var hit in hits)
            {
                if (!TryGetHitBalloon(hit.collider, out _, out var balloonModel))
                {
                    continue;
                }

                if (!balloonModel.IsTough() || AlreadyPlowed(balloonModel))
                {
                    continue;
                }

                pending.Add(new PendingPierceHit(balloonModel, hit.collider.transform.position));
            }

            if (pending.Count > 0)
            {
                _model.Flight.PierceWasRainbow = _model.HasBuff(ProjectileBuffId.RainbowShield);
            }
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
                Log.Warn("ProjectileView",
                    $"TryGetHitBalloon: BalloonView on " +
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
            var t = Mathf.Repeat(_rainbowGlowTimer * _visual.RainbowGlowSpeed, 1f);
            ApplyGlow(ColorCycle.Sample(_paletteColors, t).WithAlpha(_visual.GlowAlpha));
        }

        private void ActivateInitialGlow()
        {
            var target = string.IsNullOrEmpty(_model.ColorName.Value)
                ? _palette.GetColor(GamePalette.SparksColorId).WithAlpha(_visual.GlowAlpha)
                : _palette.GetColor(_model.ColorName.Value).WithAlpha(_visual.GlowAlpha);
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
                ? _palette.GetColor(GamePalette.SparksColorId).WithAlpha(_visual.GlowAlpha)
                : _palette.GetColor(_model.ColorName.Value).WithAlpha(_visual.GlowAlpha);
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
            _glowTween = DOTween.To(() => _glowColor, ApplyGlow, target, _visual.GlowColorDuration);
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

#if UNITY_EDITOR
        private const float SweepGizmoLineWidth = 10f;
        private const float SweepGizmoBounceRadius = 0.12f;

        private static readonly Color SweepCountingColor = new(0.8f, 0f, 0f, 1f);
        private static readonly Color SweepPostThresholdColor = new(0f, 0.2f, 0.8f, 1f);

        private readonly List<Vector3> _sweepGizmoWarmupPath = new();
        private readonly List<Vector3> _sweepGizmoPostPath = new();

        private bool _sweepGizmoTracking;
        private bool _sweepGizmoThresholdReached;

        private void OnDrawGizmos()
        {
            // Always draw the warm-up path in red once it exists.
            if (_sweepGizmoWarmupPath.Count >= 2)
            {
                Handles.color = SweepCountingColor;
                Handles.DrawAAPolyLine(SweepGizmoLineWidth, _sweepGizmoWarmupPath.ToArray());
                Gizmos.color = SweepCountingColor;
                for (var i = 0; i < _sweepGizmoWarmupPath.Count; i++)
                {
                    Gizmos.DrawSphere(_sweepGizmoWarmupPath[i], SweepGizmoBounceRadius);
                }
            }

            // Draw the post-threshold path in blue.
            if (_sweepGizmoPostPath.Count >= 2)
            {
                Handles.color = SweepPostThresholdColor;
                Handles.DrawAAPolyLine(SweepGizmoLineWidth, _sweepGizmoPostPath.ToArray());
                Gizmos.color = SweepPostThresholdColor;
                for (var i = 0; i < _sweepGizmoPostPath.Count; i++)
                {
                    Gizmos.DrawSphere(_sweepGizmoPostPath[i], SweepGizmoBounceRadius);
                }
            }

            // Live tail extending from the last recorded point.
            if (_sweepGizmoTracking && _hasFlown)
            {
                var activePath = _sweepGizmoThresholdReached ? _sweepGizmoPostPath : _sweepGizmoWarmupPath;
                if (activePath.Count > 0)
                {
                    var tailColor = _sweepGizmoThresholdReached ? SweepPostThresholdColor : SweepCountingColor;
                    var liveEnd = (Vector3)transform.position;
                    Handles.color = new Color(tailColor.r, tailColor.g, tailColor.b, 0.5f);
                    Handles.DrawAAPolyLine(SweepGizmoLineWidth * 0.6f, activePath[activePath.Count - 1], liveEnd);
                }
            }
        }

        private void StartSweepGizmoTracking()
        {
            if (_sweepGizmoTracking)
            {
                return;
            }

            _sweepGizmoTracking = true;
            _sweepGizmoWarmupPath.Add(_model.Flight.LastBouncePosition);
        }

        private void RecordSweepGizmoBounce(Vector3 wallHitPosition)
        {
            if (!_sweepGizmoTracking)
            {
                return;
            }

            if (_sweepGizmoThresholdReached)
            {
                _sweepGizmoPostPath.Add(wallHitPosition);
            }
            else
            {
                _sweepGizmoWarmupPath.Add(wallHitPosition);
            }
        }

        private void ResetSweepGizmo()
        {
            _sweepGizmoWarmupPath.Clear();
            _sweepGizmoPostPath.Clear();
            _sweepGizmoTracking = false;
            _sweepGizmoThresholdReached = false;
        }
#endif
    }
}
