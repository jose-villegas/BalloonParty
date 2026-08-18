using System;
using BalloonParty.Configuration.Items;
using BalloonParty.Configuration.Palette;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.Messages;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using MessagePipe;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Item.Snipe
{
    /// <summary>
    ///     The rainbow Snipe's discharge payoff: when a rainbow lance shatters the tough balloons it plowed
    ///     through, every paintable balloon near the shattered line turns rainbow too, in a radius that
    ///     grows with how many toughs it ate (capped so a big plow never eats the whole board) — except
    ///     balloons ahead of where the shot is still travelling. The toughs themselves are never painted;
    ///     they're the fuel that was just spent, not the reward.
    /// </summary>
    /// <remarks>
    ///     The forward exclusion exists because the shot isn't necessarily done: a banked Snipe charge — a
    ///     second Snipe pickup grabbed while one lance is already flying, saved up instead of wasted — can
    ///     arm a fresh piercing run the instant this discharge ends, sending the shot straight back through
    ///     whatever lies ahead, including balloons this bloom just converted. Popping a balloon the shot
    ///     painted itself would hand it a free, uncapped streak hit (<c>ColorStreakTracker.RecordWildcard</c>),
    ///     so the bloom stays behind the shot instead of feeding its own combo. This is the same fix already
    ///     made to the plain per-pop rainbow-neighbour conversion
    ///     (<c>ProjectileHitResolver.ConvertSideNeighboursToRainbow</c>, which recolours the balloons beside
    ///     a normal pop), which had the identical self-feeding problem before it was limited to the shot's
    ///     sides.
    /// </remarks>
    internal sealed class SnipeDischargeBloom : IStartable, IDisposable
    {
        private readonly ISubscriber<PierceDischargedMessage> _dischargedSubscriber;
        private readonly IItemConfiguration _itemConfig;
        private readonly SlotGrid _grid;

        private IDisposable _subscription;

        internal SnipeDischargeBloom(
            ISubscriber<PierceDischargedMessage> dischargedSubscriber,
            IItemConfiguration itemConfig,
            SlotGrid grid)
        {
            _dischargedSubscriber = dischargedSubscriber;
            _itemConfig = itemConfig;
            _grid = grid;
        }

        public void Start()
        {
            _subscription = _dischargedSubscriber.Subscribe(OnDischarged);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void OnDischarged(PierceDischargedMessage msg)
        {
            if (!msg.IsRainbow)
            {
                return;
            }

            var snipe = _itemConfig[ItemType.Snipe].Snipe;
            var charge = msg.ToughCount * snipe.ChargePerToughHit;
            var radius = Mathf.Min(snipe.BloomBaseRadius + charge * snipe.BloomRadiusPerCharge, snipe.BloomRadiusCap);

            var direction2D = new Vector2(msg.Direction.x, msg.Direction.y);

            foreach (var slot in _grid.AllOccupiedSlots())
            {
                if (_grid.At(slot) is not IPaintable paintable)
                {
                    continue;
                }

                var worldPosition = _grid.IndexToWorldPosition(slot);
                if (!worldPosition.WithinRadius(msg.Center, radius))
                {
                    continue;
                }

                // Anchored at the shot's actual position (where it resumes travel from), NOT the plowed
                // line's centre used for the radius above — the centre sits behind the shot by discharge
                // time, so a centre-anchored test would exclude the far side of the bloom instead of the
                // near side the shot is about to fly back through. A zero-length direction (no onward
                // travel, e.g. the death-flush discharge) reads as "ahead" of nothing, so IsAhead never
                // excludes — the bloom falls back to the full radius.
                if (((Vector2)worldPosition - (Vector2)msg.Position).IsAhead(direction2D))
                {
                    continue;
                }

                paintable.Color.Value = GamePalette.RainbowColorId;
            }
        }
    }
}
