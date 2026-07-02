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
        private readonly IHitDispatcher _hitDispatcher;
        private readonly ISubscriber<TransformCapturedMessage> _transformCapturedSubscriber;
        private readonly IItemConfiguration _itemConfig;
        private readonly List<RaycastHit2D> _castResults = new(4);
        private readonly HashSet<IBalloonModel> _hitModels = new();
        private readonly DisturbanceFieldService _disturbanceField;

        // Legitimately cross-activation: rotations arrive via TransformCapturedMessage before the
        // activation reaches this handler, keyed per balloon and consumed once on activation.
        private readonly Dictionary<ISlotActor, Quaternion> _capturedRotations = new();

        public ItemType Type => ItemType.Laser;

        [Inject]
        internal LaserItemHandler(
            IItemConfiguration itemConfig,
            IHitDispatcher hitDispatcher,
            ISubscriber<TransformCapturedMessage> transformCapturedSubscriber,
            ItemEffectPlayer effectPlayer,
            BalloonOverlapQuery overlap,
            DisturbanceFieldService disturbanceField)
        {
            _itemConfig = itemConfig;
            _hitDispatcher = hitDispatcher;
            _transformCapturedSubscriber = transformCapturedSubscriber;
            _effectPlayer = effectPlayer;
            _overlap = overlap;
            _disturbanceField = disturbanceField;
        }

        public void Start()
        {
            _transformCapturedSubscriber.Subscribe(msg => _capturedRotations[msg.Source] = msg.Snapshot.Rotation);
        }

        public UniTask Activate(IBalloonModel balloon, Vector3 worldPosition)
        {
            var settings = _itemConfig[ItemType.Laser];

            _capturedRotations.TryGetValue(balloon, out var laserRotation);
            _capturedRotations.Remove(balloon);

            CastCross(balloon, worldPosition, settings, laserRotation);
            _effectPlayer.Play(settings, worldPosition, laserRotation, balloon.GetColorId());
            StampCross(worldPosition, settings, laserRotation);

            return UniTask.CompletedTask;
        }

        private void CastCross(IBalloonModel balloon, Vector3 worldPosition, ItemSettings settings, Quaternion laserRotation)
        {
            var radius = settings.Laser.CircleCastRadius;
            var distance = settings.Laser.RaycastDistance;
            var context =
                new DamageContext(settings.Damage, settings.Flags, balloon.GetColorId());

            var right = laserRotation * Vector3.right;
            var left = laserRotation * Vector3.left;
            var up = laserRotation * Vector3.up;
            var down = laserRotation * Vector3.down;

            _hitModels.Clear();

            CastDirection(balloon, worldPosition, right, radius, distance, context, _hitModels);
            CastDirection(balloon, worldPosition, left, radius, distance, context, _hitModels);
            CastDirection(balloon, worldPosition, up, radius, distance, context, _hitModels);
            CastDirection(balloon, worldPosition, down, radius, distance, context, _hitModels);
        }

        private void CastDirection(
            IBalloonModel balloon,
            Vector3 worldPosition,
            Vector2 direction,
            float radius,
            float distance,
            DamageContext context,
            HashSet<IBalloonModel> hitModels)
        {
            var count = Physics2D.CircleCast(worldPosition, radius, direction, _overlap.Filter, _castResults, distance);

            for (var i = 0; i < count; i++)
            {
                if (!_overlap.TryResolveBalloon(_castResults[i].collider, balloon, out var balloonView, out var model))
                {
                    continue;
                }

                if (!hitModels.Add(model))
                {
                    continue;
                }

                _hitDispatcher.Dispatch(ActorHitMessage.From(model,
                    balloonView.transform.position,
                    Vector3.zero,
                    context));
            }
        }

        private void StampCross(Vector3 worldPosition, ItemSettings settings, Quaternion laserRotation)
        {
            var distance = settings.Laser.RaycastDistance;
            var profile = _disturbanceField.GetProfile(StampSource.Laser);
            var step = profile.Radius * 1.5f;
            var steps = Mathf.Max(1, Mathf.CeilToInt(distance / step));

            var right = (Vector2)(laserRotation * Vector3.right);
            var left = (Vector2)(laserRotation * Vector3.left);
            var up = (Vector2)(laserRotation * Vector3.up);
            var down = (Vector2)(laserRotation * Vector3.down);

            StampArm(worldPosition, right, steps, step);
            StampArm(worldPosition, left, steps, step);
            StampArm(worldPosition, up, steps, step);
            StampArm(worldPosition, down, steps, step);
        }

        private void StampArm(Vector3 worldPosition, Vector2 direction, int steps, float step)
        {
            for (var i = 0; i <= steps; i++)
            {
                var pos = (Vector2)worldPosition + direction * (step * i);
                _disturbanceField.Stamp(StampSource.Laser, pos, direction);
            }
        }
    }
}
