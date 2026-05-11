#region

using BalloonParty.Balloon.Model;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;

#endregion

namespace BalloonParty.Item.Bomb
{
    public class BombItemHandler : IBalloonItem
    {
        private static readonly int BalloonsLayer = LayerMask.GetMask("Balloons");

        private readonly IGameConfiguration _config;
        private readonly ItemConfiguration _itemConfig;
        private readonly IPublisher<BalloonHitMessage> _hitPublisher;
        private readonly PoolManager _poolManager;

        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Bomb;

        [Inject]
        public BombItemHandler(
            IGameConfiguration config,
            ItemConfiguration itemConfig,
            IPublisher<BalloonHitMessage> hitPublisher,
            PoolManager poolManager)
        {
            _config = config;
            _itemConfig = itemConfig;
            _hitPublisher = hitPublisher;
            _poolManager = poolManager;
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        public UniTask Activate()
        {
            var settings = _itemConfig[ItemType.Bomb];

            BlastBalloons(settings.BombRadius);
            SpawnVisual(settings);

            return UniTask.CompletedTask;
        }

        private void BlastBalloons(float radius)
        {
            var results = Physics2D.OverlapCircleAll(_worldPosition, radius, BalloonsLayer);

            if (results == null || results.Length == 0)
            {
                return;
            }

            foreach (var col in results)
            {
                var balloonView = col.GetComponentInParent<BalloonView>();
                if (balloonView == null || balloonView.Model == null)
                {
                    continue;
                }

                // Skip the bomb balloon itself — it is already being handled by the
                // normal hit pipeline that triggered this activation.
                if (balloonView.Model == _balloon)
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
            vfx.Play(_worldPosition, balloonColor, () => _poolManager.Return(key, vfx));
        }
    }
}

