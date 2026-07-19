using System;
using BalloonParty.Projectile.Buffs;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using Cysharp.Threading.Tasks;
using MessagePipe;
using VContainer;
using VContainer.Unity;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     Arms the active projectile as a piercing lance: it plows through balloons like a cruise-earned
    ///     pierce but without entering cruise (so it never gains the per-shield speed tap), and carries a
    ///     single speed buff until the pierce is spent. Plowing a tough actor bleeds the speed back toward
    ///     base (the motion resolver's pierce decay), and the next wall then ends the pierce. Pure buff
    ///     grant: no damage, no shield change.
    /// </summary>
    internal class SnipeItemHandler : IBalloonItem, IStartable, IDisposable
    {
        private readonly IItemConfiguration _itemConfig;
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IProjectileBuffs _buffs;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;

        private IWriteableProjectileModel _activeProjectile;
        private IDisposable _subscription;

        public ItemType Type => ItemType.Snipe;

        [Inject]
        internal SnipeItemHandler(
            IItemConfiguration itemConfig,
            ItemEffectPlayer effectPlayer,
            IProjectileBuffs buffs,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _itemConfig = itemConfig;
            _effectPlayer = effectPlayer;
            _buffs = buffs;
            _loadedSubscriber = loadedSubscriber;
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
            var settings = _itemConfig[ItemType.Snipe];

            if (_activeProjectile != null)
            {
                _activeProjectile.IsPiercing.Value = true;

                // Non-stacking: while a speed buff is already riding the shot a second Snipe only re-arms
                // the (idempotent) pierce, it doesn't compound the speed. Once the pierce is spent the buff
                // is gone, so a later Snipe grants afresh.
                if (!_activeProjectile.HasBuff(ProjectileBuffId.Speed))
                {
                    _buffs.Apply(new ProjectileBuff(
                        ProjectileBuffId.Speed, settings.Snipe.SpeedBuffMultiplier, BuffModifierOp.Multiplicative,
                        new PierceEndedEndCondition(_activeProjectile.IsPiercing)));
                }
            }

            _effectPlayer.Play(settings, activation.WorldPosition, activation.Balloon.GetColorId());
            return UniTask.CompletedTask;
        }
    }
}
