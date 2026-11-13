using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Where a snake's head steps next: on along its heading when that slot is open, else the gentlest open
    ///     turn, right before left. It never turns back, and its own body fills the slots behind it, so it never
    ///     walks over itself. Nor does it step where its body would link the board's unbreakables side to side.
    /// </summary>
    internal static class SnakeWalk
    {
        // Straight on, then 60° right and left, then 120° right and left.
        private static readonly int[] Turns = { 0, 1, -1, 2, -2 };

        /// <summary>
        ///     The direction the head at <paramref name="head" />, walking <paramref name="heading" />, steps in. False
        ///     when every slot ahead is off the board, below <paramref name="lastRow" />, taken, or would close
        ///     <paramref name="wall" /> once the head is there and the slot of <paramref name="tail" /> is free. A
        ///     null wall (the board already walled by something else) checks nothing.
        /// </summary>
        internal static bool TryStep(
            SlotGrid grid, Vector2Int head, Vector2Int tail, int heading, int lastRow, ShotWall wall,
            out int direction)
        {
            for (var i = 0; i < Turns.Length; i++)
            {
                var turned = HexCoordinates.Turn(heading, Turns[i]);
                var next = HexCoordinates.Step(head, turned);
                if (grid.InBounds(next) && next.y <= lastRow && grid.IsEmpty(next.x, next.y)
                    && !ClosesWall(wall, next, tail))
                {
                    direction = turned;
                    return true;
                }
            }

            direction = heading;
            return false;
        }

        private static bool ClosesWall(ShotWall wall, Vector2Int next, Vector2Int tail)
        {
            if (wall == null)
            {
                return false;
            }

            wall.Set(tail, false);
            wall.Set(next, true);
            var closes = wall.Spans();
            wall.Set(next, false);
            wall.Set(tail, true);
            return closes;
        }
    }
}
