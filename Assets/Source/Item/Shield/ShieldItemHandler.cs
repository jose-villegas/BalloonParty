#region

using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;

#endregion

namespace BalloonParty.Item.Shield
{
    public class ShieldItemHandler : IBalloonItem, IStartable
    {
        private readonly IGameConfiguration _config;
        private readonly ItemConfiguration _itemConfig;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly PoolManager _poolManager;

        private IWriteableProjectileModel _activeProjectile;
        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        public ItemType Type => ItemType.Shield;

        [Inject]
        public ShieldItemHandler(
            IGameConfiguration config,
            ItemConfiguration itemConfig,
            PoolManager poolManager,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _config = config;
            _itemConfig = itemConfig;
            _poolManager = poolManager;
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
