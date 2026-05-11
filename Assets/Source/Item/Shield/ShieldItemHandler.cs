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
    /// <summary>
    ///     Handles activation of <see cref="ItemType.Shield" /> balloons.
    ///     Increments <see cref="IWriteableProjectileModel.ShieldsRemaining" /> on the active projectile
    ///     and plays the <c>PSVFX_ShieldGainPU</c> particle at the balloon's world position.
    /// </summary>
    public class ShieldItemHandler : IBalloonItem, IStartable
    {
        private readonly IGameConfiguration _config;
        private readonly PoolManager _poolManager;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;

        private IWriteableProjectileModel _activeProjectile;
        private IBalloonModel _balloon;
        private Vector3 _worldPosition;

        [Inject]
        public ShieldItemHandler(
            IGameConfiguration config,
            PoolManager poolManager,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _config = config;
            _poolManager = poolManager;
            _loadedSubscriber = loadedSubscriber;
        }

        // ---- IStartable ----

        public void Start()
        {
            _loadedSubscriber.Subscribe(msg => _activeProjectile = (IWriteableProjectileModel)msg.Model);
        }

        // ---- IBalloonItem ----

        public ItemType Type => ItemType.Shield;

        public void Setup(IBalloonModel balloon, Vector3 worldPosition)
        {
            _balloon = balloon;
            _worldPosition = worldPosition;
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

        // ---- Private ----

        private void PlayVfx()
        {
            var settings = _config.ItemConfiguration[ItemType.Shield];
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

