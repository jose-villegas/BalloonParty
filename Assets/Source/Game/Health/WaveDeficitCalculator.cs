using UnityEngine;

namespace BalloonParty.Game.Health
{
    /// <summary>
    ///     Pure, stateless calculator for the line-based damage formula.
    ///     Each heart represents one full spawn line; partial-line deficits are forgiven.
    /// </summary>
    internal static class WaveDeficitCalculator
    {
        /// <summary>
        ///     Computes how many hearts a spawn wave costs given the grid's available space.
        /// </summary>
        /// <param name="availableSpace">Empty slots in the grid.</param>
        /// <param name="neededSlots">Total slots the wave wants to fill (<c>spawnLines × rowLength</c>).</param>
        /// <param name="rowLength">Columns in the grid — one full row = one heart.</param>
        /// <returns>Deficit breakdown: hearts lost (full lines only) and total unspawned slots.</returns>
        internal static WaveDeficit Calculate(int availableSpace, int neededSlots, int rowLength)
        {
            if (rowLength <= 0)
            {
                return default;
            }

            var deficit = Mathf.Max(0, neededSlots - availableSpace);

            if (deficit == 0)
            {
                return default;
            }

            var heartsLost = deficit / rowLength;
            return new WaveDeficit(heartsLost, deficit);
        }
    }
}
