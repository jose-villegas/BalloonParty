using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item.Laser
{
    public class LaserItemHandler : IBalloonItem, IStartable
    {
        private static readonly int BalloonsLayer = LayerMask.GetMask("Balloons");

        private readonly IGameConfiguration _config;
        private readonly ItemConfiguration _itemConfig;
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly ISubscriber<ItemRotationCapturedMessage> _rotationSubscriber;
        private readonly PoolManager _poolManager;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;
        private Quaternion _laserRotation;

        public ItemType Type => ItemType.Laser;

        [Inject]
        public LaserItemHandler(
            IGameConfiguration config,
            ItemConfiguration itemConfig,
            IPublisher<BalloonHitMessage> hitPublisher,
            ISubscriber<ItemRotationCapturedMessage> rotationSubscriber,
            PoolManager poolManager)
        {
            _config = config;
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _rotationSubscriber = rotationSubscriber;
            _poolManager = poolManager;
        }

        public void Start()
        {
            _rotationSubscriber.Subscribe(msg => _laserRotation = msg.Rotation);
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        public UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Laser];

            CastCross(settings);
            SpawnVisual(settings);

            return UniTask.CompletedTask;
        }

        private void CastCross(ItemSettings settings)
        {
            var radius = settings.LaserCircleCastRadius;
            var distance = settings.LaserRaycastDistance;

            var right = _laserRotation * Vector3.right;
            var left = _laserRotation * Vector3.left;
            var up = _laserRotation * Vector3.up;
            var down = _laserRotation * Vector3.down;

            var hitModels = new HashSet<IBalloonModel>();

            CastDirection(right, radius, distance, hitModels);
            CastDirection(left, radius, distance, hitModels);
            CastDirection(up, radius, distance, hitModels);
            CastDirection(down, radius, distance, hitModels);
        }

        private void CastDirection(
            Vector2 direction,
            float radius,
            float distance,
            HashSet<IBalloonModel> hitModels)
        {
            var results = Physics2D.CircleCastAll(_worldPosition, radius, direction, distance, BalloonsLayer);

            foreach (var hit in results)
            {
                var balloonView = hit.collider.GetComponentInParent<BalloonView>();
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

                _hitPublisher.Publish(new BalloonHitMessage(balloonView.Model, balloonView.transform.position));
            }
        }

        private void SpawnVisual(ItemSettings settings)
        {
            if (settings.ActivationVfxPrefab == null)
            {
                return;
            }

            var key = settings.ActivationVfxPrefab.name;
            var vfx = _poolManager.GetOrRegister(key, () => new VfxPoolChannel(settings.ActivationVfxPrefab));

            var balloonColor = _config.BalloonColor(_balloon.Color.Value);
            vfx.Play(_worldPosition, _laserRotation, balloonColor, () => _poolManager.Return(key, vfx));
        }
    }
}
