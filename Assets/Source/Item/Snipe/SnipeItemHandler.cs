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
    ///     projectile's hit/motion resolvers, not here). A pickup landing on an already-piercing shot is
    ///     banked whole and activates at that discharge instead — two pierces can't overlap, so the grant
    ///     is saved, not wasted. Non-damaging: no damage, no shield change.
    /// </summary>
    internal class SnipeItemHandler : IBalloonItem, IStartable, IDisposable
    {
        private readonly IItemConfiguration _itemConfig;
        private readonly IGamePalette _palette;
        private readonly ItemEffectPlayer _effectPlayer;
        private readonly IProjectileBuffs _buffs;
        private readonly ISubscriber<ProjectileLoadedMessage> _loadedSubscriber;
        private readonly ISubscriber<PierceDischargedMessage> _dischargedSubscriber;

        private IWriteableProjectileModel _activeProjectile;
        private IDisposable _subscription;
        private IDisposable _dischargedSubscription;

        public ItemType Type => ItemType.Snipe;

        [Inject]
        internal SnipeItemHandler(
            IItemConfiguration itemConfig,
            IGamePalette palette,
            ItemEffectPlayer effectPlayer,
            IProjectileBuffs buffs,
            ISubscriber<ProjectileLoadedMessage> loadedSubscriber,
            ISubscriber<PierceDischargedMessage> dischargedSubscriber)
        {
            _itemConfig = itemConfig;
            _palette = palette;
            _effectPlayer = effectPlayer;
            _buffs = buffs;
            _loadedSubscriber = loadedSubscriber;
            _dischargedSubscriber = dischargedSubscriber;
        }

        public void Start()
        {
            _subscription = _loadedSubscriber.Subscribe(msg => _activeProjectile = (IWriteableProjectileModel)msg.Model);
            _dischargedSubscription = _dischargedSubscriber.Subscribe(_ => TrySpendBankedCharge());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _dischargedSubscription?.Dispose();
        }

        public UniTask Activate(ItemActivationContext activation)
        {
            var settings = _itemConfig[ItemType.Snipe];
            var isRainbowHost = _palette.IsRainbow(activation.Balloon.GetColorId());

            if (_activeProjectile != null)
            {
                // Two pierces can't overlap, so a pickup landing on an already-armed shot is BANKED whole
                // — pierce, speed and rainbow together — and activates at the discharge that spends the
                // current pierce (TrySpendBankedCharge). Granting the speed buff here instead would
                // compound it onto the ramp the running pierce already earned.
                if (_activeProjectile.IsPiercing.Value)
                {
                    if (isRainbowHost)
                    {
                        _activeProjectile.Flight.BankedRainbowPierceCharges++;
                    }
                    else
                    {
                        _activeProjectile.Flight.BankedPierceCharges++;
                    }
                }
                else
                {
                    ArmLance(settings.Snipe.SpeedBuffMultiplier, isRainbowHost);
                }
            }

            _effectPlayer.Play(settings, activation.WorldPosition, activation.Balloon.GetColorId());
            return UniTask.CompletedTask;
        }

        // The discharge spent the pierce but SpendPierce kept the shot armed for a banked charge (so the
        // level-up hold keyed off IsPiercing never releases mid-flight) — activate that saved pickup now.
        // A shot that died on the discharge wall has already overspent its shields: its charges die with
        // it, matching "piercing re-arms as long as the projectile still has shields".
        private void TrySpendBankedCharge()
        {
            if (_activeProjectile == null || _activeProjectile.ShieldsRemaining.Value < 0
                || !_activeProjectile.IsPiercing.Value)
            {
                return;
            }

            var flight = _activeProjectile.Flight;
            var isRainbowCharge = flight.BankedRainbowPierceCharges > 0;
            if (isRainbowCharge)
            {
                flight.BankedRainbowPierceCharges--;
            }
            else if (flight.BankedPierceCharges > 0)
            {
                flight.BankedPierceCharges--;
            }
            else
            {
                return;
            }

            ArmLance(_itemConfig[ItemType.Snipe].Snipe.SpeedBuffMultiplier, isRainbowCharge);
        }

        // IsPiercing may already be true here — a banked charge re-arms a shot the discharge deliberately
        // left armed — and the write is idempotent either way (ReactiveProperty dedupes, so no subscriber
        // sees a spurious edge).
        private void ArmLance(float speedBuffMultiplier, bool isRainbowHost)
        {
            _activeProjectile.IsPiercing.Value = true;

            // Non-stacking: while a speed buff is already riding the shot a second lance only re-arms the
            // (idempotent) pierce, it doesn't compound the speed. Once the pierce is spent the buff is
            // gone, so a later Snipe grants afresh.
            if (!_activeProjectile.HasBuff(ProjectileBuffId.Speed))
            {
                _buffs.Apply(new ProjectileBuff(
                    ProjectileBuffId.Speed, speedBuffMultiplier, BuffModifierOp.Multiplicative,
                    new PierceEndedEndCondition(_activeProjectile.IsPiercing)));
            }

            // A rainbow host turns the lance iridescent: it scores colour-agnostically and rainbow-converts
            // popped balloons' neighbours as it pierces (the shared RainbowShield-buff path), and its
            // discharge blooms a colour conversion scaled by the toughs it plowed. Tied to the pierce, so
            // it ends with the discharge alongside the speed buff.
            // Applied unguarded, unlike the speed buff: RainbowShield is a flag (value 0), so a second
            // instance can't compound the way a multiplier would — while skipping it left the lance riding
            // whatever grant landed FIRST, and a rainbow Shield item's is ended by a plain wall bounce
            // (WallBounceEndCondition), clipping the iridescence long before the pierce it belongs to.
            // Redundant instances simply expire together. This also matches the solver, which tracks the
            // wall-ended and pierce-ended grants as two independent flags (ShotFlightState).
            if (isRainbowHost)
            {
                _buffs.Apply(new ProjectileBuff(
                    ProjectileBuffId.RainbowShield, 0f, BuffModifierOp.Flat,
                    new PierceEndedEndCondition(_activeProjectile.IsPiercing)));
            }
        }
    }
}
