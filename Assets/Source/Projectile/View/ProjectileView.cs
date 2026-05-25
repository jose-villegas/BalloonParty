using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Game.Score;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile.View
{
    public class ProjectileView : MonoBehaviour, IPoolable
    {
        private static int BalloonsLayer = -1;

        [Header("Glow")] [SerializeField] private SpriteRenderer _glowRenderer;

        [SerializeField] [Range(0f, 1f)] private float _glowAlpha = 0.5f;
        [SerializeField] private float _glowColorDuration = 0.2f;

        [Inject] private GamePalette _palette;
        [Inject] private IGameConfiguration _config;
        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        [Inject] private IPublisher<ActorHitMessage> _hitPublisher;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        [Inject] private ISubscriber<BalloonDeflectedMessage> _deflectedSubscriber;
        [Inject] private ColorStreakTracker _streakTracker;
        [Inject] private PauseService _pauseService;

        private IWriteableProjectileModel _model;
        private IDisposable _deflectedSubscription;
        private ProjectileTrail _projectileTrail;
        private bool _shieldShown;
        private ProjectileShieldView _shieldView;

        private void Awake()
        {
            // NameToLayer cannot be called from a static field initializer on a MonoBehaviour.
            if (BalloonsLayer == -1)
            {
                BalloonsLayer = LayerMask.NameToLayer("Balloons");
            }

            _shieldView = GetComponentInChildren<ProjectileShieldView>(true);
            _projectileTrail = GetComponentInChildren<ProjectileTrail>(true);
        }

        private void FixedUpdate()
        {
            if (_model == null || !_model.IsFree || _pauseService.IsAnyPaused.Value)
            {
                return;
            }

            RevealShieldOnFirstFreeFrame();
            MoveAndBounce();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_model == null || !_model.IsFree || _pauseService.IsAnyPaused.Value)
            {
                return;
            }

            if (!TryGetHitBalloon(other, out var balloonView, out var balloonModel))
            {
                return;
            }

            _model.LastHitBalloon = balloonModel;

            var damageContext = new DamageContext(1, DamageFlags.Normal, _model.ColorName.Value);
            var outcome = balloonModel.EvaluateHit(damageContext);

            if (outcome == HitOutcome.Absorb)
            {
                OnAbsorb(balloonModel, balloonView.transform.position);
                return;
            }

            if (outcome == HitOutcome.Pop && balloonModel is IHasColor colorable &&
                !string.IsNullOrEmpty(colorable.Color.Value))
            {
                UpdateProjectileColor(colorable.Color.Value);
            }

            _hitPublisher.Publish(new ActorHitMessage(balloonModel,
                balloonView.transform.position,
                _model.Direction,
                outcome,
                damageContext));

            // ScoreController processes the message synchronously above — tracker is already updated.
            if (outcome == HitOutcome.Pop && balloonModel is IHasColor &&
                _streakTracker.CurrentStreak >= 2 &&
                _streakTracker.LastColor == _model.ColorName.Value)
            {
                _model.ShieldsRemaining.Value++;
                _shieldGainedPublisher.Publish(new ShieldGainedMessage(_model.LastHitBalloon.SlotIndex.Value));
            }
        }

        public void OnSpawned()
        {
            _shieldShown = false;
            _deflectedSubscription?.Dispose();
            _deflectedSubscription = null;
        }

        public void OnDespawned()
        {
            _deflectedSubscription?.Dispose();
            _deflectedSubscription = null;
            _model = null;
            _shieldShown = false;
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

        private void UpdateProjectileColor(string hitColor)
        {
            if (_model.ColorName.Value == hitColor)
            {
                return;
            }

            _model.ColorName.Value = hitColor;
            UpdateGlowColor();
        }

        private Vector3 ClampToLimits(Vector3 pos, out Vector3 reflect)
        {
            reflect = Vector3.zero;
            var limits = _config.LimitsClockwise;

            if (pos.y > limits.x)
            {
                reflect += Vector3.down;
                pos.y = limits.x;
            }

            if (pos.x > limits.y)
            {
                reflect += Vector3.left;
                pos.x = limits.y;
            }

            if (pos.y < limits.z)
            {
                reflect += Vector3.up;
                pos.y = limits.z;
            }

            if (pos.x < limits.w)
            {
                reflect += Vector3.right;
                pos.x = limits.w;
            }

            return pos;
        }

        // Separated so the absorb terminal path is testable without physics.
        internal void OnAbsorb(ISlotActor actor, Vector3 worldPos)
        {
            _hitPublisher.Publish(new ActorHitMessage(actor, worldPos, _model.Direction, HitOutcome.Absorb));
            _model.IsFree = false;
            DestroyProjectile();
        }

        private void DestroyProjectile()
        {
            _projectileTrail?.Disable();
            _balancePublisher.Publish(default);
            _destroyedPublisher.Publish(default);
        }

        private void MoveAndBounce()
        {
            var pos = transform.position;
            pos += _model.Direction * (_model.Speed * Time.fixedDeltaTime);
            pos = ClampToLimits(pos, out var reflect);

            if (reflect != Vector3.zero)
            {
                _model.ShieldsRemaining.Value--;
                PlayBounceEffect(pos);

                if (_model.ShieldsRemaining.Value < 0)
                {
                    DestroyProjectile();
                    return;
                }

                _model.Direction = Vector2.Reflect(_model.Direction, reflect.normalized);
            }

            transform.position = pos;
            transform.up = _model.Direction;
        }

        private void OnBalloonDeflected(BalloonDeflectedMessage msg)
        {
            if (_model == null || msg.Balloon != _model.LastHitBalloon)
            {
                return;
            }

            var surfaceNormal = ((Vector2)transform.position - (Vector2)msg.BalloonWorldPosition).normalized;
            _model.Direction = Vector2.Reflect(_model.Direction, surfaceNormal);
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

            balloonView = other.GetComponentInParent<BalloonView>();
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

        private void UpdateGlowColor()
        {
            if (_glowRenderer == null || string.IsNullOrEmpty(_model.ColorName.Value))
            {
                return;
            }

            var color = _palette.GetColor(_model.ColorName.Value);
            _glowRenderer.DOColor(new Color(color.r, color.g, color.b, _glowAlpha), _glowColorDuration);
        }
    }
}
