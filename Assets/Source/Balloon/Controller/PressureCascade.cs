using System.Collections.Generic;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Grid;
using UnityEngine;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>Finds the shortest chain of cells a pressure shove must displace to reach a free slot.</summary>
    internal static class PressureCascade
    {
        // Doubled coordinates give every hex direction a constant step. Order: E, W, NE, NW, SE, SW.
        private static readonly Vector2Int[] DoubledDirections =
        {
            new(2, 0), new(-2, 0),
            new(1, -1), new(-1, -1),
            new(1, 1), new(-1, 1)
        };

        // Reused across calls, cleared at entry, to avoid per-call allocations during overflow crunch.
        private static readonly Dictionary<Vector2Int, Vector2Int> Parents = new();
        private static readonly HashSet<Vector2Int> Visited = new();
        private static readonly Queue<Vector2Int> SearchQueue = new();

        internal static bool TryFindChain(SlotGrid grid, int startColumn, List<Vector2Int> chain)
        {
            chain.Clear();

            // The first non-traversable occupant is what must move, not the literal bottom cell.
            if (!TryFindLowestBlocker(grid, startColumn, out var start)
                || grid.At(start) is not IPressureMovable)
            {
                return false;
            }

            Parents.Clear();
            Visited.Clear();
            SearchQueue.Clear();
            Visited.Add(start);
            SearchQueue.Enqueue(start);

            while (SearchQueue.Count > 0)
            {
                var current = SearchQueue.Dequeue();
                if (grid.At(current) is not IPressureMovable mover)
                {
                    continue;
                }

                // Relocating actors vacate to a free slot, ending the chain.
                if (mover.PushResponse != PressureResponse.ShoveNeighbour
                    && TryRelocationTarget(grid, current, mover.PushResponse, out var destination))
                {
                    BuildChain(Parents, start, current, chain);
                    chain.Add(destination);
                    return true;
                }

                if (TryShoveToEmptyNeighbour(grid, current, start, Parents, Visited, SearchQueue, chain))
                {
                    return true;
                }
            }

            return false;
        }

        // Rays out in each hex direction; finalises the chain if a ray reaches an empty cell.
        private static bool TryShoveToEmptyNeighbour(
            SlotGrid grid, Vector2Int current, Vector2Int start,
            Dictionary<Vector2Int, Vector2Int> parents, HashSet<Vector2Int> visited,
            Queue<Vector2Int> queue, List<Vector2Int> chain)
        {
            foreach (var direction in DoubledDirections)
            {
                if (!TryRayToShovableCell(grid, current, direction, out var next))
                {
                    continue;
                }

                if (grid.IsEmpty(next.x, next.y))
                {
                    BuildChain(parents, start, current, chain);
                    chain.Add(next);
                    return true;
                }

                if (visited.Add(next))
                {
                    parents[next] = current;
                    queue.Enqueue(next);
                }
            }

            return false;
        }

        // The first non-traversable occupant walking up from the entry.
        private static bool TryFindLowestBlocker(SlotGrid grid, int col, out Vector2Int blocker)
        {
            for (var row = grid.Rows - 1; row >= 0; row--)
            {
                if (!grid.IsEmpty(col, row) && !grid.IsTraversable(col, row))
                {
                    blocker = new Vector2Int(col, row);
                    return true;
                }
            }

            blocker = default;
            return false;
        }

        // Rays through pass-through occupants to the first empty or shovable cell.
        private static bool TryRayToShovableCell(
            SlotGrid grid,
            Vector2Int from,
            Vector2Int doubledDirection,
            out Vector2Int result)
        {
            result = default;

            var xd = (2 * from.x) + (from.y & 1);
            var yd = from.y;

            while (true)
            {
                xd += doubledDirection.x;
                yd += doubledDirection.y;

                var row = yd;
                var col = (xd - (yd & 1)) / 2;

                if (!grid.InBounds(col, row))
                {
                    return false;
                }

                if (grid.IsEmpty(col, row))
                {
                    result = new Vector2Int(col, row);
                    return true;
                }

                var occupant = grid.At(new Vector2Int(col, row));
                if (occupant is IPressureMovable)
                {
                    result = new Vector2Int(col, row);
                    return true;
                }

                if (occupant is not IPassThrough)
                {
                    return false;
                }
            }
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
