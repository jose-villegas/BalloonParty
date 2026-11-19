using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Where a snake's head steps next: on along its heading when that slot is open, else the gentlest open
    ///     turn, right before left. A slot is open when empty or held by a balloon the snake trades places with
    ///     (<see cref="CanPassThrough" />). It never turns back, and its own body fills the slots behind it, so it never
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
                if (grid.InBounds(next) && next.y <= lastRow && IsOpen(grid, next)
                    && !ClosesWall(wall, next, tail))
                {
                    direction = turned;
                    return true;
                }
            }

            direction = heading;
            return false;
        }

        /// <summary>
        ///     The deepest row a snake walks on a board of <paramref name="rows" />: it keeps a horizontal line's
        ///     rows, never in the bottom <see cref="IUnbreakableFormationSettings.HorizontalBottomClearance" />.
        /// </summary>
        internal static int LastRow(int rows, IUnbreakableFormationSettings settings)
        {
            return rows - 1 - Mathf.Max(settings.HorizontalBottomClearance, settings.BottomClearance);
        }

        /// <summary>
        ///     Whether a snake may walk into <paramref name="occupant" />'s slot, sending it to the slot its tail
        ///     leaves: a balloon that moves, held by nothing. A formation member (a tough or an unbreakable,
        ///     free or not), an anchored actor (the balloon a cover stands round, an armed nuke) and anything
        ///     static stand in its way.
        /// </summary>
        internal static bool CanPassThrough(IWriteableSlotActor occupant)
        {
            return occupant is IWriteableDynamicSlotActor
                   && occupant is not IFormationMember
                   && occupant is not IBalanceAnchor { IsAnchored: true };
        }

        private static bool IsOpen(SlotGrid grid, Vector2Int slot)
        {
            return grid.IsEmpty(slot.x, slot.y) || CanPassThrough(grid.At(slot));
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
