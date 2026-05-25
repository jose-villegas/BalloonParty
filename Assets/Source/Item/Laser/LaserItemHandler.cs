using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
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
        private static readonly int BalloonsLayer = LayerMask.GetMask("Balloons");

        private readonly ContactFilter2D _balloonFilter;
        private readonly GamePalette _palette;
        private readonly IPublisher<ActorHitMessage> _hitPublisher;
        private readonly ISubscriber<TransformCapturedMessage> _transformCapturedSubscriber;
        private readonly ItemConfiguration _itemConfig;
        private readonly List<RaycastHit2D> _castResults = new(4);
        private readonly PoolManager _poolManager;

        private readonly Dictionary<ISlotActor, Quaternion> _capturedRotations = new();

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Laser;

        [Inject]
        internal LaserItemHandler(
            GamePalette palette,
            ItemConfiguration itemConfig,
            IPublisher<ActorHitMessage> hitPublisher,
            ISubscriber<TransformCapturedMessage> transformCapturedSubscriber,
            PoolManager poolManager)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _transformCapturedSubscriber = transformCapturedSubscriber;
            _poolManager = poolManager;

            _balloonFilter = new ContactFilter2D();
            _balloonFilter.SetLayerMask(BalloonsLayer);
            _balloonFilter.useTriggers = true;
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
            SpawnVisual(settings, laserRotation);

            return UniTask.CompletedTask;
        }

        private void CastCross(ItemSettings settings, Quaternion laserRotation)
        {
            var radius = settings.LaserCircleCastRadius;
            var distance = settings.LaserRaycastDistance;
            var context =
                new DamageContext(settings.Damage, settings.Flags, (_balloon as IHasColor)?.Color.Value ?? "");

            var right = laserRotation * Vector3.right;
            var left = laserRotation * Vector3.left;
            var up = laserRotation * Vector3.up;
            var down = laserRotation * Vector3.down;

            var hitModels = new HashSet<IBalloonModel>();

            CastDirection(right, radius, distance, context, hitModels);
            CastDirection(left, radius, distance, context, hitModels);
            CastDirection(up, radius, distance, context, hitModels);
            CastDirection(down, radius, distance, context, hitModels);
        }

        private void CastDirection(
            Vector2 direction,
            float radius,
            float distance,
            DamageContext context,
            HashSet<IBalloonModel> hitModels)
        {
            var count = Physics2D.CircleCast(_worldPosition, radius, direction, _balloonFilter, _castResults, distance);

            for (var i = 0; i < count; i++)
            {
                var balloonView = _castResults[i].collider.GetComponentInParent<BalloonView>();
                if (balloonView == null || balloonView.Model == null)
                {
                    continue;
                }

                if (balloonView.Model == _balloon)
                {
                    continue;
                }

                if (!hitModels.Add(balloonView.Model))
                {
                    continue;
                }

                _hitPublisher.Publish(new ActorHitMessage(balloonView.Model,
                    balloonView.transform.position,
                    Vector3.zero,
                    balloonView.Model.EvaluateHit(context),
                    context));
            }
        }

        private void SpawnVisual(ItemSettings settings, Quaternion laserRotation)
        {
            if (settings.ActivationEffectPrefab == null)
            {
                return;
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key, () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var balloonColor = _palette.GetColor((_balloon as IHasColor)?.Color.Value);
            effect.Play(_worldPosition, laserRotation, balloonColor, () => _poolManager.Return(key, effect));
        }
    }
}
