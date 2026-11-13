using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Where a snake's head steps next: on along its heading when that slot is open, else the gentlest open
    ///     turn, right before left. It never turns back, and its own body fills the slots behind it, so it never
    ///     walks over itself.
    /// </summary>
    internal static class SnakeWalk
    {
        // Straight on, then 60° right and left, then 120° right and left.
        private static readonly int[] Turns = { 0, 1, -1, 2, -2 };

        /// <summary>
        ///     The direction the head at <paramref name="head" />, walking <paramref name="heading" />, steps in. False
        ///     when every slot ahead is off the board, below <paramref name="lastRow" />, or taken.
        /// </summary>
        internal static bool TryStep(SlotGrid grid, Vector2Int head, int heading, int lastRow, out int direction)
        {
            for (var i = 0; i < Turns.Length; i++)
            {
                var turned = HexCoordinates.Turn(heading, Turns[i]);
                var next = HexCoordinates.Step(head, turned);
                if (grid.InBounds(next) && next.y <= lastRow && grid.IsEmpty(next.x, next.y))
                {
                    direction = turned;
                    return true;
                }
            }

            direction = heading;
            return false;
        }
    }
}
