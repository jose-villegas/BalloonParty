using BalloonParty.Projectile.Buffs;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Grants the active projectile a <see cref="SpeedProjectileBuff" /> — increases its flight speed
    ///     (multiplier from config) until it loses a shield to a wall. Purely a buff grant: no damage, no shield change.
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
            _buffs.Apply(new SpeedProjectileBuff(_wallBounces, settings.Snipe.SpeedBuffMultiplier));
            _effectPlayer.Play(settings, activation.WorldPosition, activation.Balloon.GetColorId());
            return UniTask.CompletedTask;
        }
    }
}
