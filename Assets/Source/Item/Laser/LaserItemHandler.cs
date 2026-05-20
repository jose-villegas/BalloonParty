using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots;
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

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;
        private Quaternion _laserRotation;

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
            _transformCapturedSubscriber.Subscribe(msg => _laserRotation = msg.Snapshot.Rotation);
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
            var damage = settings.Damage;

            var right = _laserRotation * Vector3.right;
            var left = _laserRotation * Vector3.left;
            var up = _laserRotation * Vector3.up;
            var down = _laserRotation * Vector3.down;

            var hitModels = new HashSet<IBalloonModel>();

            CastDirection(right, radius, distance, damage, hitModels);
            CastDirection(left, radius, distance, damage, hitModels);
            CastDirection(up, radius, distance, damage, hitModels);
            CastDirection(down, radius, distance, damage, hitModels);
        }

        private void CastDirection(
            Vector2 direction,
            float radius,
            float distance,
            int damage,
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
                    balloonView.Model is IHitable h ? h.EvaluateHit(damage) : HitOutcome.PassThrough,
                    damage));
            }
        }

        private void SpawnVisual(ItemSettings settings)
        {
            if (settings.ActivationEffectPrefab == null)
            {
                return;
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key, () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var balloonColor = _palette.GetColor(_balloon.Color.Value);
            effect.Play(_worldPosition, _laserRotation, balloonColor, () => _poolManager.Return(key, effect));
        }
    }
}
