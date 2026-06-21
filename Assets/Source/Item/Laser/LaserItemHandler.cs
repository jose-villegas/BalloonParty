using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item.Laser
{
    internal class LaserItemHandler : IBalloonItem, IStartable
    {
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly BalloonOverlapQuery _overlap;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly ISubscriber<TransformCapturedMessage> _transformCapturedSubscriber;
        private readonly IItemConfiguration _itemConfig;
        private readonly List<RaycastHit2D> _castResults = new(4);
        private readonly HashSet<IBalloonModel> _hitModels = new();
        private readonly DisturbanceFieldService _disturbanceField;

        private readonly Dictionary<ISlotActor, Quaternion> _capturedRotations = new();

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Laser;

        [Inject]
        internal LaserItemHandler(
            IItemConfiguration itemConfig,
            IPublisher<ActorHitMessage> hitPublisher,
            ISubscriber<TransformCapturedMessage> transformCapturedSubscriber,
            ItemEffectPlayer effectPlayer,
            BalloonOverlapQuery overlap,
            DisturbanceFieldService disturbanceField)
        {
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _transformCapturedSubscriber = transformCapturedSubscriber;
            _effectPlayer = effectPlayer;
            _overlap = overlap;
            _disturbanceField = disturbanceField;
        }

        public void Start()
        {
            _transformCapturedSubscriber.Subscribe(msg => _capturedRotations[msg.Source] = msg.Snapshot.Rotation);
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        public UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Laser];

            _capturedRotations.TryGetValue(_balloon, out var laserRotation);
            _capturedRotations.Remove(_balloon);

            CastCross(settings, laserRotation);
            _effectPlayer.Play(settings, _worldPosition, laserRotation, _balloon.GetColorId());
            StampCross(settings, laserRotation);

            return UniTask.CompletedTask;
        }

        private void CastCross(ItemSettings settings, Quaternion laserRotation)
        {
            var radius = settings.LaserCircleCastRadius;
            var distance = settings.LaserRaycastDistance;
            var context =
                new DamageContext(settings.Damage, settings.Flags, _balloon.GetColorId());

            var right = laserRotation * Vector3.right;
            var left = laserRotation * Vector3.left;
            var up = laserRotation * Vector3.up;
            var down = laserRotation * Vector3.down;

            _hitModels.Clear();

            CastDirection(right, radius, distance, context, _hitModels);
            CastDirection(left, radius, distance, context, _hitModels);
            CastDirection(up, radius, distance, context, _hitModels);
            CastDirection(down, radius, distance, context, _hitModels);
        }

        private void CastDirection(
            Vector2 direction,
            float radius,
            float distance,
            DamageContext context,
            HashSet<IBalloonModel> hitModels)
        {
            var count = Physics2D.CircleCast(_worldPosition, radius, direction, _overlap.Filter, _castResults, distance);

            for (var i = 0; i < count; i++)
            {
                if (!_overlap.TryResolveBalloon(_castResults[i].collider, _balloon, out var balloonView, out var model))
                {
                    continue;
                }

                if (!hitModels.Add(model))
                {
                    continue;
                }

                _hitPublisher.Publish(ActorHitMessage.From(model,
                    balloonView.transform.position,
                    Vector3.zero,
                    context));
            }
        }

        private void StampCross(ItemSettings settings, Quaternion laserRotation)
        {
            var distance = settings.LaserRaycastDistance;
            var profile = _disturbanceField.GetProfile(StampSource.Laser);
            var step = profile.Radius * 1.5f;
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / step));

            var right = (Vector2)(laserRotation * Vector3.right);
            var left = (Vector2)(laserRotation * Vector3.left);
            var up = (Vector2)(laserRotation * Vector3.up);
            var down = (Vector2)(laserRotation * Vector3.down);

            StampArm(right, steps, step);
            StampArm(left, steps, step);
            StampArm(up, steps, step);
            StampArm(down, steps, step);
        }

        private void StampArm(Vector2 direction, int steps, float step)
        {
            for (var i = 0; i <= steps; i++)
            {
                var pos = (Vector2)_worldPosition + direction * (step * i);
                _disturbanceField.Stamp(StampSource.Laser, pos, direction);
            }
        }
    }
}
