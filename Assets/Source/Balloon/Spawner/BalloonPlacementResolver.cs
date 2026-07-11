using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>Picks the slot a column's new balloon should take, falling back to nearby columns under pressure.</summary>
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

            // Cached to avoid a per-call delegate allocation.
            _resolveOpenEntry = ResolveOpenEntry;
            _resolvePressureOpen = ResolvePressureOpen;
        }

        /// <summary>Read-only probe of the column's next entry row — used to order a line's columns by depth.</summary>
        public int? ProbeEntryRow(int col)
        {
            return FindFirstReachableEmptyRow(col);
        }

        /// <summary><paramref name="allowReject"/> false (initial fill) restricts to the column's own entry.</summary>
        public Vector2Int? Resolve(int col, bool allowReject)
        {
            var ownRow = FindFirstReachableEmptyRow(col);
            if (ownRow.HasValue)
            {
                return new Vector2Int(col, ownRow.Value);
            }

            // Initial fill never saturates, so only turn spawns search beyond the column.
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

        /// <summary>Topmost empty row reachable from below without passing a non-traversable actor (e.g. a bush).</summary>
        private int? FindFirstReachableEmptyRow(int col)
        {
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

            for (var row = ceilingRow + 1; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    return row;
                }
            }

            return null;
        }

        // Scans columns nearest-first from fromCol; startDistance 0 includes the column itself, 1 skips it.
        private bool TryNearestColumn(
            int fromCol,
            int startDistance,
            Func<int, Vector2Int?> resolve,
            out Vector2Int target)
        {
            for (var distance = startDistance; distance < _grid.Columns; distance++)
            {
                if (TryAtDistance(fromCol, distance, resolve, out target))
                {
                    return true;
                }
            }

            target = default;
            return false;
        }

        // Checks the column(s) `distance` away from `fromCol`, left neighbour before right.
        private bool TryAtDistance(int fromCol, int distance, Func<int, Vector2Int?> resolve, out Vector2Int target)
        {
            if (distance == 0)
            {
                return TryColumn(fromCol, resolve, out target);
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
