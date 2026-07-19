using System;
using BalloonParty.Configuration.Palette;
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
    ///     Handles the Snipe item: arms the active projectile as a piercing lance — it plows through
    ///     balloons like a cruise-earned pierce, but without entering cruise (so it skips cruise's
    ///     per-shield speed tap) — and grants a single, non-stacking multiplicative
    ///     <see cref="ProjectileBuffId.Speed" /> buff. A rainbow-balloon host also arms the shared
    ///     <see cref="ProjectileBuffId.RainbowShield" /> buff. Both buffs ride the pierce and end together
    ///     when the shared pierce-discharge fires (the plow-then-shatter mechanic itself lives in the
    ///     projectile's hit/motion resolvers, not here). Non-damaging: no damage, no shield change.
    /// </summary>
    internal class SnipeItemHandler : IBalloonItem, IStartable, IDisposable
    {
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IProjectileBuffs _buffs;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;

        private IWriteableProjectileModel _activeProjectile;
        private IDisposable _subscription;

        public ItemType Type => ItemType.Snipe;

        [Inject]
        internal SnipeItemHandler(
            IItemConfiguration itemConfig,
            IGamePalette palette,
            ItemEffectPlayer effectPlayer,
            IProjectileBuffs buffs,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber)
        {
            _itemConfig = itemConfig;
            _palette = palette;
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

                // A rainbow host turns the lance iridescent: it scores colour-agnostically and rainbow-converts
                // popped balloons' neighbours as it pierces (the shared RainbowShield-buff path), and its
                // discharge blooms a colour conversion scaled by the toughs it plowed. Tied to the pierce, so
                // it ends with the discharge alongside the speed buff.
                if (_palette.IsRainbow(activation.Balloon.GetColorId())
                    && !_activeProjectile.HasBuff(ProjectileBuffId.RainbowShield))
                {
                    _buffs.Apply(new ProjectileBuff(
                        ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                        new PierceEndedEndCondition(_activeProjectile.IsPiercing)));
                }
            }

            _effectPlayer.Play(settings, activation.WorldPosition, activation.Balloon.GetColorId());
            return UniTask.CompletedTask;
        }
    }
}
