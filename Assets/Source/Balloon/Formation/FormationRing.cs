using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     A ring's geometry: its members hold neighbours of one slot, its centre, and it turns by each member
    ///     taking the next neighbour round, clockwise or the other way (<see cref="HexCoordinates.Step" />
    ///     numbers the six from east, clockwise). One neighbour at least is always open, so a shot can reach the
    ///     centre through the gap as it comes round. Pure: it reads the grid and plans moves, and moves nothing.
    /// </summary>
    internal static class FormationRing
    {
        /// <summary>Of the centre's six neighbours one stays open.</summary>
        internal const int MostMembers = HexCoordinates.Directions - 1;

        /// <summary>
        ///     Whether a ring can stand round <paramref name="centre" />: all six of its neighbours are on the board
        ///     and no deeper than <paramref name="lastRow" />, so it can turn all the way round, and the centre
        ///     itself is empty or holds a balloon nothing else holds.
        /// </summary>
        internal static bool IsCentre(SlotGrid grid, Vector2Int centre, int lastRow)
        {
            if (!grid.InBounds(centre) || !SnakeWalk.IsOpen(grid, centre))
            {
                return false;
            }

            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                var neighbour = HexCoordinates.Step(centre, direction);
                if (!grid.InBounds(neighbour) || neighbour.y > lastRow)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        ///     Plans one turn into <paramref name="moves" />: each member to the next neighbour of
        ///     <paramref name="centre" />, and each balloon a member lands on to a slot the ring leaves. False,
        ///     with no moves, when a member is not beside the centre, when a slot the ring would turn into holds
        ///     something it can't trade places with (<see cref="SnakeWalk.CanPassThrough" />), or when the turn
        ///     would close <paramref name="wall" /> (null checks nothing).
        /// </summary>
        internal static bool TryTurn(
            SlotGrid grid, Vector2Int centre, IReadOnlyList<IWriteableFormationMember> members, bool clockwise,
            ShotWall wall, List<SlotMove> moves)
        {
            moves.Clear();
            var turn = clockwise ? 1 : -1;
            var held = 0;
            for (var i = 0; i < members.Count; i++)
            {
                var around = HexCoordinates.DirectionBetween(centre, members[i].SlotIndex.Value);
                if (around < 0)
                {
                    return false;
                }

                held |= 1 << around;
            }

            for (var i = 0; i < members.Count; i++)
            {
                var next = HexCoordinates.Turn(HexCoordinates.DirectionBetween(centre, members[i].SlotIndex.Value), turn);
                var target = HexCoordinates.Step(centre, next);

                // A slot a fellow member holds is free by the time this one lands: the batch lifts every mover first.
                if ((held & 1 << next) == 0 && (!grid.InBounds(target) || !SnakeWalk.IsOpen(grid, target)))
                {
                    moves.Clear();
                    return false;
                }

                moves.Add(new SlotMove(members[i], target));
            }

            if (ClosesWall(centre, held, turn, wall))
            {
                moves.Clear();
                return false;
            }

            MoveBalloonsRound(grid, centre, held, turn, moves);
            return true;
        }

        /// <summary>
        ///     The direction a member at the centre's neighbour <paramref name="around" /> moves in as the ring
        ///     turns: along the ring, not out from the centre.
        /// </summary>
        internal static int Tangent(int around, bool clockwise)
        {
            return HexCoordinates.Turn(around, clockwise ? 2 : -2);
        }

        /// <summary>
        ///     Whether the member bound for <paramref name="target" /> stands at one end of the ring's arc, with an
        ///     open slot on one side of it only: then <paramref name="clockwise" /> is the way that leads it into
        ///     the open slot, so the arc follows it round.
        /// </summary>
        internal static bool LeadsAt(
            Vector2Int centre, IReadOnlyList<FormationAssignment> assignments, Vector2Int target, out bool clockwise)
        {
            clockwise = false;
            var around = HexCoordinates.DirectionBetween(centre, target);
            if (around < 0)
            {
                return false;
            }

            var held = 0;
            for (var i = 0; i < assignments.Count; i++)
            {
                var direction = HexCoordinates.DirectionBetween(centre, assignments[i].Target);
                if (direction >= 0)
                {
                    held |= 1 << direction;
                }
            }

            var openClockwise = (held & 1 << HexCoordinates.Turn(around, 1)) == 0;
            var openOtherWay = (held & 1 << HexCoordinates.Turn(around, -1)) == 0;
            clockwise = openClockwise;
            return openClockwise != openOtherWay;
        }

        // The ring's unbreakables off the slots it leaves and onto those it turns into, then back.
        private static bool ClosesWall(Vector2Int centre, int held, int turn, ShotWall wall)
        {
            if (wall == null)
            {
                return false;
            }

            Shift(centre, held, turn, wall, true);
            var closes = wall.Spans();
            Shift(centre, held, turn, wall, false);
            return closes;
        }

        private static void Shift(Vector2Int centre, int held, int turn, ShotWall wall, bool turned)
        {
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                if ((held & 1 << direction) == 0)
                {
                    continue;
                }

                // A slot at an arc's tail is left empty, and the one past its head is taken.
                if ((held & 1 << HexCoordinates.Turn(direction, -turn)) == 0)
                {
                    wall.Set(HexCoordinates.Step(centre, direction), !turned);
                }

                if ((held & 1 << HexCoordinates.Turn(direction, turn)) == 0)
                {
                    wall.Set(HexCoordinates.Step(centre, direction + turn), turned);
                }
            }
        }

        // A balloon in a slot the ring turns into goes to the nearest slot the ring leaves: each arc takes one slot
        // at its head and leaves one at its tail, so there is always a slot for each.
        private static void MoveBalloonsRound(SlotGrid grid, Vector2Int centre, int held, int turn, List<SlotMove> moves)
        {
            var left = 0;
            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                if ((held & 1 << direction) != 0 && (held & 1 << HexCoordinates.Turn(direction, -turn)) == 0)
                {
                    left |= 1 << direction;
                }
            }

            for (var direction = 0; direction < HexCoordinates.Directions; direction++)
            {
                if ((held & 1 << direction) != 0 || (held & 1 << HexCoordinates.Turn(direction, -turn)) == 0)
                {
                    continue;
                }

                var taken = HexCoordinates.Step(centre, direction);
                if (grid.IsEmpty(taken.x, taken.y) || grid.At(taken) is not IWriteableDynamicSlotActor balloon)
                {
                    continue;
                }

                var nearest = -1;
                for (var candidate = 0; candidate < HexCoordinates.Directions; candidate++)
                {
                    if ((left & 1 << candidate) != 0
                        && (nearest < 0
                            || HexCoordinates.Distance(HexCoordinates.Step(centre, candidate), taken)
                            < HexCoordinates.Distance(HexCoordinates.Step(centre, nearest), taken)))
                    {
                        nearest = candidate;
                    }
                }

                if (nearest >= 0)
                {
                    moves.Add(new SlotMove(balloon, HexCoordinates.Step(centre, nearest)));
                    left &= ~(1 << nearest);
                }
            }
        }
    }
}
