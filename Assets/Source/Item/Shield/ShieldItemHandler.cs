using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item.Shield
{
    internal class ShieldItemHandler : IBalloonItem, IStartable
    {
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly IItemConfiguration _itemConfig;

        private IWriteableProjectileModel _activeProjectile;
        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Shield;

        [Inject]
        internal ShieldItemHandler(
            IItemConfiguration itemConfig,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ItemEffectPlayer effectPlayer)
        {
            _itemConfig = itemConfig;
            _shieldGainedPublisher = shieldGainedPublisher;
            _loadedSubscriber = loadedSubscriber;
            _effectPlayer = effectPlayer;
        }

        public void Start()
        {
            _loadedSubscriber.Subscribe(msg => _activeProjectile = (IWriteableProjectileModel)msg.Model);
        }

        public UniTask Activate()
        {
            if (_activeProjectile != null)
            {
                _activeProjectile.ShieldsRemaining.Value++;
            }

            _shieldGainedPublisher.Publish(new ShieldGainedMessage(_balloon.SlotIndex.Value));
            _effectPlayer.Play(_itemConfig[ItemType.Shield], _worldPosition, _balloon.GetColorId());
            return UniTask.CompletedTask;
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }
    }
}
