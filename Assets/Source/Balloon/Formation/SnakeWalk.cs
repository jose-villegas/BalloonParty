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
    ///     walks over itself. Nor does it step where its body would link the board's unbreakables side to side. The
    ///     toughs' snake keeps to one row instead (<see cref="TryStepTowardRow" />), and its head may fold back
    ///     beside its body; the body still fills the slot right behind it.
    /// </summary>
    internal static class SnakeWalk
    {
        // Straight on, then 60° right and left, then 120° right and left.
        private static readonly int[] Turns = { 0, 1, -1, 2, -2 };

        // A snake that keeps to one row, by the way it leans (east: heading east, south-east or north-east). Below
        // the row: up first, the way it leans, then along its own row, then down as a last resort. On the row:
        // along it only, unless boxed in at both ends, when it dips to the row below to get round. Above it: down
        // to it, then along.
        private static readonly int[] ClimbEast = { 5, 4, 0, 3, 1, 2 };
        private static readonly int[] ClimbWest = { 4, 5, 3, 0, 2, 1 };
        private static readonly int[] SlideEast = { 0, 3 };
        private static readonly int[] SlideWest = { 3, 0 };
        private static readonly int[] DipEast = { 0, 3, 1, 2 };
        private static readonly int[] DipWest = { 3, 0, 2, 1 };
        private static readonly int[] DescendEast = { 1, 2, 0, 3 };
        private static readonly int[] DescendWest = { 2, 1, 3, 0 };

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
        ///     The direction the head of a snake that keeps to <paramref name="row" /> steps in: it climbs toward the
        ///     row, and on it walks only east or west, so once its whole body is up the snake slides along the row.
        ///     False when every way it may take is off the board, below <paramref name="lastRow" />, above the row
        ///     while climbing, or taken; the caller then turns the snake round, tail leading. With
        ///     <paramref name="boxedIn" /> (both ends found no step) a head on the row may also step down off it, and
        ///     climbs back on the next step.
        /// </summary>
        internal static bool TryStepTowardRow(
            SlotGrid grid, Vector2Int head, int heading, int lastRow, int row, bool boxedIn, out int direction)
        {
            var east = heading is 0 or 1 or 5;
            var order = head.y > row ? east ? ClimbEast : ClimbWest
                : head.y < row ? east ? DescendEast : DescendWest
                : boxedIn ? east ? DipEast : DipWest
                : east ? SlideEast : SlideWest;
            for (var i = 0; i < order.Length; i++)
            {
                var next = HexCoordinates.Step(head, order[i]);
                if (grid.InBounds(next) && next.y <= lastRow && (head.y < row || next.y >= row)
                    && IsOpen(grid, next))
                {
                    direction = order[i];
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

        /// <summary>Whether a formation may move into <paramref name="slot" />: empty, or held by a balloon it trades places with.</summary>
        internal static bool IsOpen(SlotGrid grid, Vector2Int slot)
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
