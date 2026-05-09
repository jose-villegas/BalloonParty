using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
using DG.Tweening;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace BalloonParty.Projectile.View
{
    public class ProjectileView : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _glowRenderer;
        [SerializeField] [Range(0f, 1f)] private float _glowAlpha = 0.5f;
        [SerializeField] private float _glowColorDuration = 0.2f;
        [Inject] private IPublisher<BalanceBalloonsMessage> _balancePublisher;

        [Inject] private IGameConfiguration _config;
        [Inject] private IPublisher<ProjectileDestroyedMessage> _destroyedPublisher;
        [Inject] private SlotGrid _grid;
        [Inject] private IPublisher<BalloonHitMessage> _hitPublisher;

        private ProjectileModel _model;

        private void FixedUpdate()
        {
            if (_model == null || !_model.IsFree) return;

            var pos = transform.position;
            pos += _model.Direction * _model.Speed * Time.fixedDeltaTime;

            var reflect = Vector3.zero;
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

            if (reflect != Vector3.zero)
            {
                _model.ShieldsRemaining.Value--;

                PlayBounceEffect(pos);

                if (_model.ShieldsRemaining.Value < 0)
                {
                    _balancePublisher.Publish(default);
                    _destroyedPublisher.Publish(default);
                    Destroy(gameObject);
                    return;
                }

                _model.Direction = Vector2.Reflect(_model.Direction, reflect.normalized);
            }

            transform.position = pos;
            transform.up = _model.Direction;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_model == null || !_model.IsFree) return;
            if (other.gameObject.layer != LayerMask.NameToLayer("Balloons")) return;

            var balloonView = other.GetComponentInParent<BalloonView>();
            if (balloonView == null) return;

            var balloonModel = balloonView.Model;
            if (balloonModel == null) return;
            if (_model.LastHitBalloon == balloonModel) return;

            _model.LastHitBalloon = balloonModel;

            TrackColor(balloonModel.Color.Value);
            NudgeNeighbors(balloonModel);
            _hitPublisher.Publish(new BalloonHitMessage(balloonModel, balloonView.transform.position));
        }

        public void Bind(ProjectileModel model)
        {
            _model = model;
        }

        private void TrackColor(string hitColor)
        {
            if (string.IsNullOrEmpty(_model.ColorName))
            {
                _model.ColorName = hitColor;
                _model.ColorPopCount = 1;
            }
            else if (_model.ColorName == hitColor)
            {
                _model.ColorPopCount++;
            }
            else
            {
                _model.ColorName = hitColor;
                _model.ColorPopCount = 1;
            }

            if (_glowRenderer != null)
            {
                var color = _config.BalloonColor(_model.ColorName);
                _glowRenderer.DOColor(new Color(color.r, color.g, color.b, _glowAlpha), _glowColorDuration);
            }
        }

        private void NudgeNeighbors(BalloonModel hitBalloon)
        {
            var hitSlot = hitBalloon.SlotIndex.Value;
            var neighbors = _grid.GetNeighbors(hitSlot.x, hitSlot.y);

            foreach (var neighbor in neighbors)
            {
                if (neighbor?.View == null) continue;

                var neighborPos = neighbor.View.transform.position;
                var direction = neighborPos - hitBalloon.View.transform.position;
                var targetSlotPos = _grid.IndexToWorldPosition(neighbor.SlotIndex.Value);

                var sequence = DOTween.Sequence();
                sequence.Append(neighbor.View.transform.DOMove(
                    neighborPos + direction.normalized * _config.NudgeDistance, _config.NudgeDuration / 2f));
                sequence.Append(neighbor.View.transform.DOMove(targetSlotPos, _config.NudgeDuration / 2f));

                neighbor.IsStable.Value = false;
                sequence.OnComplete(() => neighbor.IsStable.Value = true);
            }
        }

        private void PlayBounceEffect(Vector3 position)
        {
            // visual bounce effect placeholder — particle FX wired in Phase 6/7
        }
    }
}