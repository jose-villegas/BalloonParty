using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Controller;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Pause;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
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

        [Header("Disappear")]
        [Tooltip("Scale-down-to-zero on the last hit (shields depleted / absorbed) and on level-up dismiss.")]
        [SerializeField] private float _disappearDuration = 0.2f;
        [SerializeField] private Ease _disappearEase = Ease.InBack;

        [Inject] private IGamePalette _palette;
        [Inject] private IGameConfiguration _config;
        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private IPublisher<ShieldLostMessage> _shieldLostPublisher;
        [Inject] private ISubscriber<BalloonDeflectedMessage> _deflectedSubscriber;
        [Inject] private ProjectileHitResolver _hitResolver;
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
            transform.localScale = _baseScale;
            _deflectedSubscription?.Dispose();
            _deflectedSubscription = null;
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
            // Rebalance the board right away; the spent projectile scales down first, and only when it
            // has vanished do we signal destruction (which reloads the next projectile).
            _balancePublisher.Publish(default);
            PlayDisappear(() => _destroyedPublisher.Publish(default));
        }

        /// <summary>
        ///     Scales the projectile down to zero, then invokes <paramref name="onComplete" />. Used for
        ///     the last-hit destruction and (via the thrower) the level-up dismiss. Idempotent — a second
        ///     call while already disappearing is ignored. Runs in unscaled time so slow-mo doesn't stretch
        ///     it and it plays even while the world is frozen at dismiss.
        /// </summary>
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
            var pos = transform.position;
            pos += _model.Direction * (_model.Speed * Time.fixedDeltaTime);
            pos = new WallLimits(_config.LimitsClockwise).Clamp(pos, out var reflect);

            if (reflect != Vector3.zero)
            {
                _model.ShieldsRemaining.Value--;
                PlayBounceEffect(pos);

                if (_model.ShieldsRemaining.Value < 0)
                {
                    DestroyProjectile();
                    return;
                }

                // A shield actually absorbed this bounce (none left would have destroyed it above), so
                // fly a shield trail to the bounce point.
                _shieldLostPublisher.Publish(new ShieldLostMessage(pos));
                _model.Direction = Vector2.Reflect(_model.Direction, reflect.normalized);
            }

            transform.position = pos;
            transform.up = _model.Direction;

            _disturbanceField.Stamp(StampSource.Projectile, pos, _model.Direction);
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
            if (_glowRenderer == null || string.IsNullOrEmpty(_model.ColorName.Value))
            {
                return;
            }

            var color = _palette.GetColor(_model.ColorName.Value);
            _glowRenderer.DOColor(color.WithAlpha(_glowAlpha), _glowColorDuration);
        }
    }
}
