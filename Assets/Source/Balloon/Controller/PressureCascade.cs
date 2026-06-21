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
    ///     A shove also routes <em>through</em> pass-through obstacles (e.g. puff clouds): from a cell
    ///     the search rays out in each of the six hex directions, skipping over <see cref="IPassThrough"/>
    ///     occupants to land on the first empty or shovable slot beyond them. A non-traversable static
    ///     actor halts that ray.
    ///
    ///     Relocating actors short-circuit the chain: when the push reaches one it vacates to a free
    ///     slot anywhere, so a reachable relocator relieves pressure as long as the board has any gap.
    ///     <see cref="PressureResponse.RelocateNearest"/> (BubbleCluster) stays close;
    ///     <see cref="PressureResponse.RelocateFarthest"/> (Unbreakable) clears as far away as it can.
    ///
    ///     Returns the chain of cells <c>[entry, …, destination]</c>; the caller shifts each occupant
    ///     into the next cell. Consecutive cells may be several apart when a ray crossed a pass-through.
    /// </summary>
    internal static class PressureCascade
    {
        // Hex directions in doubled coordinates (xd = 2*col + row%2, yd = row), where every direction
        // is a constant step — unlike offset col/row, which zig-zags. Order: E, W, NE, NW, SE, SW.
        private static readonly Vector2Int[] DoubledDirections =
        {
            new(2, 0), new(-2, 0),
            new(1, -1), new(-1, -1),
            new(1, 1), new(-1, 1)
        };

        internal static bool TryFindChain(SlotGrid grid, int startColumn, List<Vector2Int> chain)
        {
            chain.Clear();

            // A rising balloon passes through puffs/empties and is stopped by the first non-traversable
            // occupant — that lowest blocker is what must move, not the literal bottom cell (which may
            // be a puff). Shoving it frees a slot the new balloon can still reach through the puffs below.
            if (!TryFindLowestBlocker(grid, startColumn, out var start)
                || grid.At(start) is not IPressureMovable)
            {
                return false;
            }

            var parents = new Dictionary<Vector2Int, Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

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

                if (TryShoveToEmptyNeighbour(grid, current, start, parents, visited, queue, chain))
                {
                    return true;
                }
            }

            return false;
        }

        // Rays out in each hex direction from <paramref name="current"/>. If a ray reaches an empty
        // cell, finalises the chain into it and returns true; otherwise enqueues newly-reached
        // shovable cells for the BFS to continue from.
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

        // Rays from <paramref name="from"/> along one hex direction, passing through pass-through
        // occupants, and reports the first empty or shovable cell. Returns false if the ray leaves the
        // grid or meets a non-traversable blocker first.
        // The first non-traversable occupant walking up from the entry — the cell a rising balloon
        // would first hit. Puffs and empties are skipped, so a puff at the bottom isn't mistaken for it.
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
