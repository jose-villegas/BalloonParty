using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Pool;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Item.Shield
{
    internal class ShieldItemHandler : IBalloonItem, IStartable
    {
        private readonly IGamePalette _palette;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly IItemConfiguration _itemConfig;
        private readonly PoolManager _poolManager;

        private IWriteableProjectileModel _activeProjectile;
        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Shield;

        [Inject]
        internal ShieldItemHandler(
            IGamePalette palette,
            IItemConfiguration itemConfig,
            PoolManager poolManager,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _palette = palette;
            _itemConfig = itemConfig;
            _poolManager = poolManager;
            _shieldGainedPublisher = shieldGainedPublisher;
            _loadedSubscriber = loadedSubscriber;
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
            PlayVfx();
            return UniTask.CompletedTask;
        }

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
        }

        private void PlayVfx()
        {
            var settings = _itemConfig[ItemType.Shield];
            if (settings.ActivationEffectPrefab == null)
            {
                return;
            }

            var key = settings.ActivationEffectPrefab.name;
            var effect = _poolManager.GetOrRegister(key, () => new EffectPoolChannel(settings.ActivationEffectPrefab));

            var balloonColor = _palette.GetColor((_balloon as IHasColor)?.Color.Value);
            effect.Play(_worldPosition, balloonColor, () => _poolManager.Return(key, effect));
        }
    }
}
