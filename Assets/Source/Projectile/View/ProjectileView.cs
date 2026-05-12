using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
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

        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;
        [Inject] private IGameConfiguration _config;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private IPublisher<BalloonHitMessage> _hitPublisher;
        [Inject] private IPublisher<ShieldGainedMessage> _shieldGainedPublisher;

        private IWriteableProjectileModel _model;
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
            if (_model == null || !_model.IsFree)
            {
                return;
            }

            RevealShieldOnFirstFreeFrame();
            MoveAndBounce();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_model == null || !_model.IsFree)
            {
                return;
            }

            if (!TryGetHitBalloon(other, out var balloonView, out var balloonModel))
            {
                return;
            }

            _model.LastHitBalloon = balloonModel;

            TrackColorStreak(balloonModel.Color.Value);
            _hitPublisher.Publish(new BalloonHitMessage(balloonModel, balloonView.transform.position));
        }

        public void OnSpawned()
        {
            _shieldShown = false;
        }

        public void OnDespawned()
        {
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
        }

        private void AwardShieldOnStreak()
        {
            if (_model.ColorPopCount >= 2)
            {
                _model.ShieldsRemaining.Value++;
                _shieldGainedPublisher.Publish(new ShieldGainedMessage(_model.LastHitBalloon.SlotIndex.Value));
            }
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

        private void PlayBounceEffect(Vector3 position)
        {
            if (_shieldView == null || string.IsNullOrEmpty(_model?.ColorName.Value))
            {
                return;
            }

            _shieldView.PlayBounceVfx(position, _config.BalloonColor(_model.ColorName.Value));
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

        private void TrackColorStreak(string hitColor)
        {
            if (string.IsNullOrEmpty(_model.ColorName.Value) || _model.ColorName.Value != hitColor)
            {
                _model.ColorName.Value = hitColor;
                _model.ColorPopCount = 1;
            }
            else
            {
                _model.ColorPopCount++;
            }

            AwardShieldOnStreak();
            UpdateGlowColor();
        }

        private bool TryGetHitBalloon(Collider2D other, out BalloonView balloonView,
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

            var color = _config.BalloonColor(_model.ColorName.Value);
            _glowRenderer.DOColor(new Color(color.r, color.g, color.b, _glowAlpha), _glowColorDuration);
        }
    }
}
