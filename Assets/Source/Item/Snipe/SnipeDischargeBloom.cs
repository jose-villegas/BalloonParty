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
    ///     The rainbow Snipe's discharge payoff: when a rainbow lance discharges, converts every paintable
    ///     balloon within a charge-scaled radius of the shattered line to rainbow. Charge is the count of
    ///     toughs it plowed; the radius grows per charge up to a hard cap so a big plow never eats the
    ///     whole board. The toughs themselves are the fuel — popped by the discharge, and never paintable.
    /// </summary>
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

            foreach (var slot in _grid.AllOccupiedSlots())
            {
                if (_grid.At(slot) is IPaintable paintable
                    && _grid.IndexToWorldPosition(slot).WithinRadius(msg.Center, radius))
                {
                    paintable.Color.Value = GamePalette.RainbowColorId;
                }
            }
        }
    }
}
