using System.Collections.Generic;
using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     Every occupied slot hosting a pierce-arming item, as circles.
    /// </summary>
    /// <remarks>
    ///     Snipe is the only item that arms piercing today — mirrors <c>SnipeItemHandler.Activate</c> and
    ///     the solver's own <c>ShotItemLayer.ResolveSnipe</c>. If a second pierce-arming item ever ships,
    ///     the check below is the one place to widen.
    ///     <para>
    ///         Walks the grid rather than a type-specific registry for the same reason as
    ///         <see cref="SlotGridDeflectorField" />: geometry comes from the spawned view, not the slot's
    ///         world position, and must not be memoised — an actor's collider drifts continuously as the
    ///         board settles, which is exactly when a player is lining a shot up.
    ///     </para>
    /// </remarks>
    internal sealed class SlotGridPierceItemField : IPierceItemField
    {
        private readonly SlotGrid _grid;

        public SlotGridPierceItemField(SlotGrid grid)
        {
            _grid = grid;
        }

        public void CollectPierceItems(List<PierceItemCircle> results)
        {
            results.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is not IHasItemSlot itemSlot || itemSlot.Item.Value != ItemType.Snipe)
                    {
                        continue;
                    }

                    var view = _grid.ViewAt(slot);
                    if (view == null || !view.HasActiveCollider || view.ContactRadius <= 0f)
                    {
                        continue;
                    }

                    results.Add(new PierceItemCircle(view.ContactCenter, view.ContactRadius));
                }
            }
        }
    }
}
