using System.Collections.Generic;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Pure grid logic for a pressure-balance push. When a column's entry can't accept a new
    ///     balloon, the arriving balloon shoves the column's bottom occupant, and that shove
    ///     propagates neighbour-to-neighbour — snake-like — until it reaches a free slot. A balloon
    ///     can be displaced in any direction (a bottleneck can force it down); the breadth-first
    ///     search finds the shortest such chain so the squeeze stays local.
    ///
    ///     Relocating actors short-circuit the chain: when the push reaches one it vacates to a free
    ///     slot anywhere, so a reachable relocator relieves pressure as long as the board has any gap.
    ///     <see cref="PressureResponse.RelocateNearest"/> (BubbleCluster) stays close;
    ///     <see cref="PressureResponse.RelocateFarthest"/> (Unbreakable) clears as far away as it can.
    ///
    ///     Returns the chain of cells <c>[entry, …, destination]</c>; the caller shifts each
    ///     occupant into the next cell. Static / pass-through occupants halt a branch — routing a
    ///     shove through traversable obstacles is a planned refinement, not handled yet.
    /// </summary>
    internal static class PressureCascade
    {
        internal static bool TryFindChain(SlotGrid grid, int startColumn, List<Vector2Int> chain)
        {
            chain.Clear();

            var start = new Vector2Int(startColumn, grid.Rows - 1);
            if (grid.At(start) is not IPressureMovable)
            {
                return false;
            }

            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            var neighbors = new Vector2Int[6];

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                if (grid.At(current) is not IPressureMovable mover)
                {
                    continue;
                }

                // Relocating actors get out of the way to a free slot, ending the chain — near or far
                // depending on the balloon's personality.
                if (mover.PushResponse != PressureResponse.ShoveNeighbour
                    && TryRelocationTarget(grid, current, mover.PushResponse, out var destination))
                {
                    BuildChain(parents, start, current, chain);
                    chain.Add(destination);
                    return true;
                }

                SlotGrid.HexNeighborIndices(current.x, current.y, neighbors);

                foreach (var next in neighbors)
                {
                    if (next.x < 0 || next.x >= grid.Columns || next.y < 0 || next.y >= grid.Rows)
                    {
                        continue;
                    }

                    if (grid.IsEmpty(next.x, next.y))
                    {
                        BuildChain(parents, start, current, chain);
                        chain.Add(next);
                        return true;
                    }

                    if (!visited.Add(next))
                    {
                        continue;
                    }

                    // Only balloons can be shoved on; static / pass-through occupants halt this branch.
                    if (grid.At(next) is IPressureMovable)
                    {
                        parents[next] = current;
                        queue.Enqueue(next);
                    }
                }
            }

            return false;
        }

        private static bool TryRelocationTarget(
            SlotGrid grid,
            Vector2Int from,
            PressureResponse response,
            out Vector2Int target)
        {
            target = default;
            var farthest = response == PressureResponse.RelocateFarthest;
            var best = farthest ? int.MinValue : int.MaxValue;
            var found = false;

            for (var col = 0; col < grid.Columns; col++)
            {
                for (var row = 0; row < grid.Rows; row++)
                {
                    if (!grid.IsEmpty(col, row))
                    {
                        continue;
                    }

                    var dx = col - from.x;
                    var dy = row - from.y;
                    var distance = (dx * dx) + (dy * dy);

                    var better = farthest ? distance > best : distance < best;
                    if (better)
                    {
                        best = distance;
                        target = new Vector2Int(col, row);
                        found = true;
                    }
                }
            }

            return found;
        }

        private static void BuildChain(
            IReadOnlyDictionary<Vector2Int, Vector2Int> parents,
            Vector2Int start,
            Vector2Int end,
            List<Vector2Int> chain)
        {
            chain.Add(end);

            var node = end;
            while (node != start)
            {
                node = parents[node];
                chain.Add(node);
            }

            chain.Reverse();
        }
    }
}
