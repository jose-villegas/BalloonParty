using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    /// Strategy for selecting which slots a grid actor type should occupy.
    /// Each actor type can provide its own logic (e.g. Puff favors adjacency
    /// for cluster formation, others might prefer isolation or specific rows).
    /// </summary>
    internal interface ISlotSelectionStrategy
    {
        /// <summary>
        /// Selects slots from the available set. Returns the chosen positions
        /// in placement order.
        /// </summary>
        /// <param name="emptySlots">All currently empty slots on the grid.</param>
        /// <param name="count">How many slots to select.</param>
        /// <returns>Selected slot positions (may be fewer than <paramref name="count"/> if not enough valid candidates).</returns>
        List<Vector2Int> SelectSlots(IReadOnlyList<Vector2Int> emptySlots, int count);
    }
}

