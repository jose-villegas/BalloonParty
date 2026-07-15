using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Grants the active projectile a speed buff (multiplier from config) until it loses a shield
    ///     to a wall. Purely a buff grant: no damage, no shield change.
    /// </summary>
    internal class SnipeItemHandler : IBalloonItem
    {
        private readonly IItemConfiguration _itemConfig;
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IProjectileBuffs _buffs;
        private readonly ISubscriber<ShieldLostMessage> _wallBounces;

        public ItemType Type => ItemType.Snipe;

        [Inject]
        internal SnipeItemHandler(
            IItemConfiguration itemConfig,
            ItemEffectPlayer effectPlayer,
            IProjectileBuffs buffs,
            ISubscriber<ShieldLostMessage> wallBounces)
        {
            _itemConfig = itemConfig;
            _effectPlayer = effectPlayer;
            _buffs = buffs;
            _wallBounces = wallBounces;
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var settings = _itemConfig[ItemType.Snipe];
            _buffs.Apply(new ProjectileBuff(
                ProjectileBuffId.Speed, settings.Snipe.SpeedBuffMultiplier,
                BuffModifierOp.Multiplicative, new WallBounceEndCondition(_wallBounces)));
            _effectPlayer.Play(settings, activation.WorldPosition, activation.Balloon.GetColorId());
            return UniTask.CompletedTask;
        }
    }
}
