using System;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item.Shield
{
    internal class ShieldItemHandler : IBalloonItem, IStartable, IDisposable
    {
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<ShieldLostMessage> _wallBounces;
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly IProjectileBuffs _buffs;

        private IWriteableProjectileModel _activeProjectile;
        private IDisposable _subscription;

        public ItemType Type => ItemType.Shield;

        [Inject]
        internal ShieldItemHandler(
            IItemConfiguration itemConfig,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<ShieldLostMessage> wallBounces,
            ItemEffectPlayer effectPlayer,
            IGamePalette palette,
            IProjectileBuffs buffs)
        {
            _itemConfig = itemConfig;
            _shieldGainedPublisher = shieldGainedPublisher;
            _loadedSubscriber = loadedSubscriber;
            _wallBounces = wallBounces;
            _effectPlayer = effectPlayer;
            _palette = palette;
            _buffs = buffs;
        }

        public void Start()
        {
            _subscription = _loadedSubscriber.Subscribe(msg => _activeProjectile = (IWriteableProjectileModel)msg.Model);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var balloon = activation.Balloon;
            var worldPosition = activation.WorldPosition;

            if (_activeProjectile != null)
            {
                _activeProjectile.ShieldsRemaining.Value++;
            }

            // A rainbow holder additionally turns the projectile iridescent AND fast. The shield it just
            // granted is what the wall consumes to end both buffs, rather than destroying the projectile.
            if (_palette.IsRainbow(balloon.GetColorId()))
            {
                var settings = _itemConfig[ItemType.Shield];
                _buffs.Apply(new ProjectileBuff(
                    ProjectileBuffId.RainbowShield, 1f, new WallBounceEndCondition(_wallBounces)));
                _buffs.Apply(new ProjectileBuff(
                    ProjectileBuffId.Speed, settings.Shield.SpeedBuffMultiplier,
                    new WallBounceEndCondition(_wallBounces)));
            }

            _shieldGainedPublisher.Publish(new ShieldGainedMessage(balloon.SlotIndex.Value));
            _effectPlayer.Play(_itemConfig[ItemType.Shield], worldPosition, balloon.GetColorId());
            return UniTask.CompletedTask;
        }
    }
}
