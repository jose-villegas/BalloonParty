using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>Lets each actor type provide its own slot-picking logic (e.g. adjacency vs. isolation).</summary>
    internal interface ISlotSelectionStrategy
    {
        /// <summary>May return fewer than <paramref name="count"/> if not enough valid candidates; maxPerCluster 0 = no limit.</summary>
        List<Vector2Int> SelectSlots(IReadOnlyList<Vector2Int> emptySlots, int count, int maxPerCluster = 0);
    }
}
