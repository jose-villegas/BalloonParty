using BalloonParty.Configuration;
using BalloonParty.Projectile;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using UnityEngine;
using VContainer;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;

namespace BalloonParty.Item.Shield
{
    internal class ShieldItemHandler : IBalloonItem
    {
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IPublisher<ShieldGainedMessage> _shieldGainedPublisher;
        private readonly ISubscriber<ShieldLostMessage> _wallBounces;
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly IProjectileBuffs _buffs;
        private readonly IProjectileRegistry _projectiles;

        public ItemType Type => ItemType.Shield;

        [Inject]
        internal ShieldItemHandler(
            IItemConfiguration itemConfig,
            IPublisher<ShieldGainedMessage> shieldGainedPublisher,
            ISubscriber<ShieldLostMessage> wallBounces,
            ItemEffectPlayer effectPlayer,
            IGamePalette palette,
            IProjectileBuffs buffs,
            IProjectileRegistry projectiles)
        {
            _itemConfig = itemConfig;
            _shieldGainedPublisher = shieldGainedPublisher;
            _wallBounces = wallBounces;
            _effectPlayer = effectPlayer;
            _palette = palette;
            _buffs = buffs;
            _projectiles = projectiles;
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var balloon = activation.Balloon;
            var worldPosition = activation.WorldPosition;

            // The shield goes to the ball that popped the holder. Activation runs a frame after the pop, so
            // that ball may already be gone: the shield then goes to the current ball rather than being
            // lost, as it always went to the next shot.
            var shot = (_projectiles.ShotOrCurrent(activation.DamageContext.Projectile)
                ?? _projectiles.ShotOrCurrent(null)) as IWriteableProjectileModel;
            if (shot != null)
            {
                shot.ShieldsRemaining.Value++;

                // A rainbow holder turns the shot iridescent. The shield it just granted is what the
                // wall consumes to end the buff, rather than destroying the shot.
                if (_palette.IsRainbow(balloon.GetColorId()))
                {
                    _buffs.Apply(shot, new ProjectileBuff(
                        ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                        new WallBounceEndCondition(_wallBounces, shot)));
                }
            }

            _shieldGainedPublisher.Publish(new ShieldGainedMessage(shot, balloon.SlotIndex.Value));
            _effectPlayer.Play(_itemConfig[ItemType.Shield], worldPosition, balloon.GetColorId());
            return UniTask.CompletedTask;
        }
    }
}
