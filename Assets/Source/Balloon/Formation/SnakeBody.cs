using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     Where the tough snake's free toughs fall in: a line of slots on from its tail, away from the body, that
    ///     they take in one batch. A slot on the line may be empty, hold a free tough that has landed, or hold a
    ///     balloon the snake can walk through (<see cref="SnakeWalk.CanPassThrough" />), which moves out to a slot a
    ///     gathered tough leaves.
    /// </summary>
    internal static class SnakeBody
    {
        // Straight on, then 60° right and left, then 120° right and left: the line bends as little as it can.
        private static readonly int[] Turns = { 0, 1, -1, 2, -2 };

        /// <summary>
        ///     Up to <paramref name="count" /> slots into <paramref name="line" />, the first a neighbour of
        ///     <paramref name="tail" /> in <paramref name="direction" /> or the gentlest turn from it, each after it a
        ///     neighbour of the one before. It keeps within the rows a snake walks (to <paramref name="lastRow" />)
        ///     and never doubles back over itself; it stops short where no slot on from its end will take a tough.
        /// </summary>
        internal static void Extend(
            SlotGrid grid, Vector2Int tail, int direction, int lastRow, int count, List<Vector2Int> line)
        {
            line.Clear();
            var end = tail;
            while (line.Count < count && TryNext(grid, end, direction, lastRow, line, out var next, out direction))
            {
                line.Add(next);
                end = next;
            }
        }

        /// <summary>A tough no snake holds that has landed: one free to fall in.</summary>
        internal static bool IsFreeLandedTough(IWriteableSlotActor occupant)
        {
            return IsFreeTough(occupant) && ((IArmouredFormationMember)occupant).IsStable.Value;
        }

        /// <summary>A tough no snake holds, landed or still gliding in.</summary>
        internal static bool IsFreeTough(IWriteableSlotActor occupant)
        {
            return occupant is IArmouredFormationMember tough && tough is not IBalanceAnchor { IsAnchored: true };
        }

        // A free tough beside the end first, whichever way it lies, so the line follows the toughs where they stand
        // and moves as few as it can; then the gentlest turn that takes one.
        private static bool TryNext(
            SlotGrid grid, Vector2Int end, int direction, int lastRow, List<Vector2Int> line, out Vector2Int next,
            out int turned)
        {
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < Turns.Length; i++)
                {
                    turned = HexCoordinates.Turn(direction, Turns[i]);
                    next = HexCoordinates.Step(end, turned);
                    if (!grid.InBounds(next) || next.y > lastRow || line.Contains(next))
                    {
                        continue;
                    }

                    var takes = pass == 0
                        ? !grid.IsEmpty(next.x, next.y) && IsFreeLandedTough(grid.At(next))
                        : Takes(grid, next);
                    if (takes)
                    {
                        return true;
                    }
                }
            }

            next = end;
            turned = direction;
            return false;
        }

        private static bool Takes(SlotGrid grid, Vector2Int slot)
        {
            if (grid.IsEmpty(slot.x, slot.y))
            {
                return true;
            }

            var occupant = grid.At(slot);
            return IsFreeLandedTough(occupant) || SnakeWalk.CanPassThrough(occupant);
        }
    }
}
