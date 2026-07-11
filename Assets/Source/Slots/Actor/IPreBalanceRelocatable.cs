using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     A dynamic actor that repositions itself before the turn's rebalance. The balancer only owns the
    ///     legal destinations and the invocation order (by <see cref="IBalanceInfluence.BalancePriority" />);
    ///     each implementer defines its own placement rule.
    /// </summary>
    internal interface IPreBalanceRelocatable
    {
        /// <summary>Picks a destination from <paramref name="restingSlots" /> (empty slots needing no further settling); false = stays put this turn.</summary>
        bool TryPickRelocation(SlotGrid grid, IReadOnlyList<Vector2Int> restingSlots, out Vector2Int target);
    }
}
