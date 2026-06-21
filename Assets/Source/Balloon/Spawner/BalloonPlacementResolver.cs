using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>
    ///     Decides which slot a line's balloon should take for a given column: its own column's reachable
    ///     entry first, then (under pressure) the nearest other column that can accept it, then a column
    ///     that pressure-balance can shove open using a gap anywhere on the board. Returns null when
    ///     nothing frees a slot — the caller then rejects (overflow pop). Resolving a pressured column has
    ///     the side effect of relieving pressure via the balancer.
    /// </summary>
    internal class BalloonPlacementResolver
    {
        private readonly SlotGrid _grid;
        private readonly BalloonBalancer _balancer;
        private readonly Func<int, Vector2Int?> _resolveOpenEntry;
        private readonly Func<int, Vector2Int?> _resolvePressureOpen;

        [Inject]
        internal BalloonPlacementResolver(SlotGrid grid, BalloonBalancer balancer)
        {
            _grid = grid;
            _balancer = balancer;

            // Cached so the nearest-column scan doesn't allocate a delegate per blocked column.
            _resolveOpenEntry = ResolveOpenEntry;
            _resolvePressureOpen = ResolvePressureOpen;
        }

        /// <summary>
        ///     The slot this column's balloon should take, or null if none can be opened.
        ///     <paramref name="allowReject"/> false (initial fill) restricts to the column's own entry.
        /// </summary>
        public Vector2Int? Resolve(int col, bool allowReject)
        {
            var ownRow = FindFirstReachableEmptyRow(col);
            if (ownRow.HasValue)
            {
                return new Vector2Int(col, ownRow.Value);
            }

            // The initial fill never saturates, so only turn spawns search beyond the column.
            if (!allowReject)
            {
                return null;
            }

            if (TryNearestColumn(col, startDistance: 1, _resolveOpenEntry, out var rehome))
            {
                return rehome;
            }

            if (TryNearestColumn(col, startDistance: 0, _resolvePressureOpen, out var pressured))
            {
                return pressured;
            }

            return null;
        }

        private int? FindFirstEmptyRowFromTop(int col)
        {
            for (var row = 0; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds the topmost empty row reachable from the spawn entry (bottom of grid).
        /// Balloons enter from below and travel upward. A non-traversable static actor
        /// (e.g. bush) blocks vertical passage — the balloon can only reach slots below
        /// the lowest blocker. This causes balloons to accumulate under bushes.
        /// </summary>
        private int? FindFirstReachableEmptyRow(int col)
        {
            // Walk from bottom of grid upward — the first non-traversable blocker is
            // the ceiling for this column. Balloons can't pass through it.
            var ceilingRow = -1;
            for (var row = _grid.Rows - 1; row >= 0; row--)
            {
                if (!_grid.IsEmpty(col, row) && !_grid.IsTraversable(col, row))
                {
                    ceilingRow = row;
                    break;
                }
            }

            if (ceilingRow < 0)
            {
                return FindFirstEmptyRowFromTop(col);
            }

            // Search for the topmost empty row below the blocker
            for (var row = ceilingRow + 1; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    return row;
                }
            }

            return null;
        }

        // Scans columns nearest-first from <paramref name="fromCol"/> (left then right at each
        // distance) and returns the first slot <paramref name="resolve"/> yields. startDistance 0
        // includes the column itself; 1 skips it.
        private bool TryNearestColumn(
            int fromCol,
            int startDistance,
            Func<int, Vector2Int?> resolve,
            out Vector2Int target)
        {
            for (var distance = startDistance; distance < _grid.Columns; distance++)
            {
                if (distance == 0)
                {
                    if (TryColumn(fromCol, resolve, out target))
                    {
                        return true;
                    }

                    continue;
                }

                var left = fromCol - distance;
                if (left >= 0 && TryColumn(left, resolve, out target))
                {
                    return true;
                }

                var right = fromCol + distance;
                if (right < _grid.Columns && TryColumn(right, resolve, out target))
                {
                    return true;
                }
            }

            target = default;
            return false;
        }

        private static bool TryColumn(int col, Func<int, Vector2Int?> resolve, out Vector2Int target)
        {
            if (resolve(col) is { } hit)
            {
                target = hit;
                return true;
            }

            target = default;
            return false;
        }

        // A column the new balloon can rise straight into.
        private Vector2Int? ResolveOpenEntry(int col)
        {
            var row = FindFirstReachableEmptyRow(col);
            return row.HasValue ? new Vector2Int(col, row.Value) : null;
        }

        // A column pressure balance can shove open by pulling a balloon into a gap anywhere on the board.
        private Vector2Int? ResolvePressureOpen(int col)
        {
            if (!_balancer.TryRelievePressure(col))
            {
                return null;
            }

            var row = FindFirstReachableEmptyRow(col);
            return row.HasValue ? new Vector2Int(col, row.Value) : null;
        }
    }
}
