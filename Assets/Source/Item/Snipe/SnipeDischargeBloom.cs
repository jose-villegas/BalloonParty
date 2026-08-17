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
    ///     balloon within a charge-scaled radius of the shattered line to rainbow, EXCEPT balloons ahead of
    ///     the shot's onward travel direction. Charge is the count of toughs it plowed; the radius grows
    ///     per charge up to a hard cap so a big plow never eats the whole board. The toughs themselves are
    ///     the fuel — popped by the discharge, and never paintable.
    /// </summary>
    /// <remarks>
    ///     The forward exclusion mirrors <c>ProjectileHitResolver.ConvertSideNeighboursToRainbow</c>: a
    ///     banked Snipe charge can re-arm piercing on this same discharge event, sending the shot straight
    ///     back through whatever it just bloomed. Without the exclusion those balloons pop as free wildcard
    ///     streak hits (uncapped <c>ColorStreakTracker.RecordWildcard</c>), so the shot's own bloom fed its
    ///     multiplier — the same self-feeding pattern the neighbour conversion had before it was scoped to
    ///     the shot's sides.
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

                // A zero-length direction (no onward travel) reads as "ahead" of nothing, so IsAhead
                // never excludes — the bloom falls back to the full radius.
                if (((Vector2)worldPosition - (Vector2)msg.Center).IsAhead(direction2D))
                {
                    continue;
                }

                paintable.Color.Value = GamePalette.RainbowColorId;
            }
        }
    }
}
