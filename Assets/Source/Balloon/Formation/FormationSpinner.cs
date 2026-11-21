using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     A spinner's geometry: a straight line of three or five through one slot, its centre, which the member
    ///     in the middle holds. It turns about that member: each of the others moves 60° round the centre at its
    ///     own distance from it, so the line goes from flat to one diagonal to the other and round again
    ///     (<see cref="HexCoordinates.Step" /> numbers the six directions from east, clockwise). Pure: it reads
    ///     the grid and plans moves, and moves nothing.
    /// </summary>
    internal static class FormationSpinner
    {
        /// <summary>The longest line: two members out on each side of the one in the middle.</summary>
        internal const int MostMembers = 5;

        /// <summary>The shortest line: one member on each side of the one in the middle.</summary>
        internal const int LeastMembers = 3;

        /// <summary>How far out from the centre the longest line's ends stand.</summary>
        internal const int Reach = MostMembers / 2;

        /// <summary>The three lines through a slot: the flat one, and the two diagonals.</summary>
        internal const int Axes = HexCoordinates.Directions / 2;

        /// <summary>
        ///     Whether a spinner with <paramref name="arm" /> members out on each side can turn all the way round
        ///     <paramref name="centre" />: every slot up to that far out along the six directions is on the board
        ///     and no deeper than <paramref name="lastRow" />, and the centre's own row is no deeper than
        ///     <paramref name="lastFlatRow" />, the deepest a line across the board may lie.
        /// </summary>
        internal static bool IsCentre(SlotGrid grid, Vector2Int centre, int arm, int lastRow, int lastFlatRow)
        {
            if (!grid.InBounds(centre) || centre.y > lastFlatRow)
            {
                return false;
            }

            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var slot = centre;
                for (var step = 1; step <= arm; step++)
                {
                    slot = HexCoordinates.Step(slot, direction);
                    if (!grid.InBounds(slot) || slot.y > lastRow)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>The slot <paramref name="distance" /> steps from <paramref name="centre" /> in <paramref name="direction" />.</summary>
        internal static Vector2Int Out(Vector2Int centre, int direction, int distance)
        {
            var slot = centre;
            for (var i = 0; i < distance; i++)
            {
                slot = HexCoordinates.Step(slot, direction);
            }

            return slot;
        }

        /// <summary>
        ///     Where <paramref name="slot" /> lies on a line through <paramref name="centre" />: its direction from
        ///     the centre and how many steps out, up to <paramref name="reach" />. False for the centre itself and
        ///     for a slot on none of the six lines.
        /// </summary>
        internal static bool TryLocate(Vector2Int centre, Vector2Int slot, int reach, out int direction, out int distance)
        {
            for (direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var along = centre;
                for (distance = 1; distance <= reach; distance++)
                {
                    along = HexCoordinates.Step(along, direction);
                    if (along == slot)
                    {
                        return true;
                    }
                }
            }

            direction = -1;
            distance = 0;
            return false;
        }

        /// <summary>
        ///     Plans one turn into <paramref name="moves" />: the member on <paramref name="centre" /> stays, each
        ///     other member goes 60° round it at its own distance, and a balloon it lands on takes the slot it
        ///     left. False, with no moves, when a member stands on none of the lines through the centre, when a
        ///     slot the line would turn into is off the board or holds something it can't trade places with
        ///     (<see cref="SnakeWalk.IsOpen" />), or when the turn would close <paramref name="wall" /> (null
        ///     checks nothing).
        /// </summary>
        internal static bool TryTurn(
            SlotGrid grid, Vector2Int centre, IReadOnlyList<IWriteableFormationMember> members, bool clockwise,
            ShotWall wall, List<SlotMove> moves)
        {
            moves.Clear();
            var turn = clockwise ? 1 : -1;
            for (var i = 0; i < members.Count; i++)
            {
                var from = members[i].SlotIndex.Value;
                if (from == centre)
                {
                    continue;
                }

                if (!TryLocate(centre, from, Reach, out var direction, out var distance))
                {
                    moves.Clear();
                    return false;
                }

                // A line never turns into a slot of its own: its members lie on one line, and the turn takes each
                // to another.
                var target = Out(centre, direction + turn, distance);
                if (!grid.InBounds(target) || !SnakeWalk.IsOpen(grid, target))
                {
                    moves.Clear();
                    return false;
                }

                moves.Add(new SlotMove(members[i], target));
            }

            if (ClosesWall(members, wall, moves))
            {
                moves.Clear();
                return false;
            }

            // The balloon each member lands on takes the slot that member left.
            var turning = moves.Count;
            for (var i = 0; i < turning; i++)
            {
                var target = moves[i].Target;
                if (!grid.IsEmpty(target.x, target.y) && grid.At(target) is IWriteableDynamicSlotActor balloon)
                {
                    moves.Add(new SlotMove(balloon, moves[i].Actor.SlotIndex.Value));
                }
            }

            return true;
        }

        // The line's unbreakables off the slots they leave and onto those they turn into, then back.
        private static bool ClosesWall(
            IReadOnlyList<IWriteableFormationMember> members, ShotWall wall, IReadOnlyList<SlotMove> moves)
        {
            if (wall == null)
            {
                return false;
            }

            Shift(wall, moves, true);
            var closes = wall.Spans();
            Shift(wall, moves, false);
            return closes;
        }

        private static void Shift(ShotWall wall, IReadOnlyList<SlotMove> moves, bool turned)
        {
            for (var i = 0; i < moves.Count; i++)
            {
                wall.Set(moves[i].Actor.SlotIndex.Value, !turned);
                wall.Set(moves[i].Target, turned);
            }
        }
    }
}
