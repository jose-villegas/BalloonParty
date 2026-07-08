using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Effects;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Projectile.View
{
    public class ProjectileView : MonoBehaviour, IPoolable
    {
        private static int BalloonsLayer = -1;

        [Header("Glow")] [SerializeField] private SpriteRenderer _glowRenderer;

        [SerializeField] [Range(0f, 1f)] private float _glowAlpha = 0.5f;
        [SerializeField] private float _glowColorDuration = 0.2f;

        [Header("Disappear")]
        [Tooltip("Scale-down-to-zero on the last hit (shields depleted / absorbed) and on level-up dismiss.")]
        [SerializeField] private float _disappearDuration = 0.2f;
        [SerializeField] private Ease _disappearEase = Ease.InBack;

        [Inject] private IGamePalette _palette;
        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private IPublisher<ShieldLostMessage> _shieldLostPublisher;
        [Inject] private ISubscriber<BalloonDeflectedMessage> _deflectedSubscriber;
        [Inject] private ProjectileHitResolver _hitResolver;
        [Inject] private ProjectileMotionResolver _motionResolver;
        [Inject] private PauseService _pauseService;
        [Inject] private DisturbanceFieldService _disturbanceField;

        private IWriteableProjectileModel _model;
        private IDisposable _deflectedSubscription;
        private ProjectileTrail _projectileTrail;
        private bool _shieldShown;
        private ProjectileShieldView _shieldView;
        private Vector3 _baseScale;
        private bool _disappearing;

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

        private void FixedUpdate()
        {
            if (_disappearing || _model == null || !_model.IsFree || _pauseService.IsAnyPaused.Value)
            {
                return;
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

        public void OnSpawned()
        {
            _shieldShown = false;
            _disappearing = false;
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
            _deflectedSubscription?.Dispose();
            _deflectedSubscription = null;
            _model = null;
            _shieldShown = false;
            _disappearing = false;
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
            // Rebalance immediately; destruction (and the next reload) waits for the scale-down.
            _balancePublisher.Publish(default);
            PlayDisappear(() => _destroyedPublisher.Publish(default));
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
            transform.DOScale(Vector3.zero, _disappearDuration)
                .SetEase(_disappearEase)
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

            _disturbanceField.Stamp(StampSource.Projectile, step.Position, step.Direction);
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

        private void UpdateGlowColor()
        {
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
    }
}
