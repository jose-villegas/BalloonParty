using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     Every occupied slot whose actor would turn an ordinary shot away, as circles.
    /// </summary>
    /// <remarks>
    ///     Walks the grid rather than a type-specific registry because deflecting is a capability, not
    ///     a family: toughs and unbreakables deflect, and so do the static deflector and a gatekeeper
    ///     with hits left. Asking the grid asks all of them at once — a new deflecting archetype needs
    ///     no change here.
    ///     <para>
    ///         Geometry comes from the spawned view, not from the slot's world position: an actor
    ///         animating into place, or one with an offset collider, deflects where its collider is.
    ///     </para>
    ///     <para>
    ///         Which is why this must NOT be memoised on <see cref="SlotGrid.MutationVersion" /> the way
    ///         MoveWeightEvaluator and ShieldReachabilityField are, however much it looks like the same
    ///         shape. Those cache occupancy, which only changes on Place/Remove. This reads positions
    ///         that drift continuously as the board settles, with no mutation to invalidate on — and a
    ///         settling board is exactly when a player is lining a shot up.
    ///     </para>
    /// </remarks>
    internal sealed class SlotGridDeflectorField : IDeflectorField
    {
        private readonly SlotGrid _grid;

        public SlotGridDeflectorField(SlotGrid grid)
        {
            _grid = grid;
        }

        public void CollectDeflectors(List<DeflectorCircle> results)
        {
            results.Clear();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var slot = new Vector2Int(col, row);
                    if (_grid.At(slot) is not IDeflectsShots deflects || !deflects.DeflectsOrdinaryHit)
                    {
                        continue;
                    }

                    var view = _grid.ViewAt(slot);
                    if (view == null || !view.HasActiveCollider || view.ContactRadius <= 0f)
                    {
                        continue;
                    }

                    results.Add(new DeflectorCircle(view.ContactCenter, view.ContactRadius));
                }
            }
        }
    }
}
