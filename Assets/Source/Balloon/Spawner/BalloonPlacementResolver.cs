using System;
using BalloonParty.Balloon.Controller;
using BalloonParty.Slots.Grid;
using UnityEngine;
using VContainer;

namespace BalloonParty.Balloon.Spawner
{
    /// <summary>How far placement may reach when a column's own entry is blocked.</summary>
    internal enum PlacementReach
    {
        /// <summary>Only the column's own reachable entry; skip when it is blocked (pop-spawn extras).</summary>
        OwnColumn,

        /// <summary>Own entry, else rehome into the nearest column with an open entry (initial fill).</summary>
        Rehome,

        /// <summary>Rehome, then shove existing balloons aside to open a slot (turn spawns).</summary>
        Pressure,
    }

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

        /// <summary>How many empty rows a rising balloon can still reach in this column (all empties below the
        /// lowest non-traversable actor, or every empty row when the column is unblocked). Diagnostics only.</summary>
        public int ReachableCapacity(int col)
        {
            var count = 0;
            for (var row = LowestBlockingRow(col) + 1; row < _grid.Rows; row++)
            {
                if (_grid.IsEmpty(col, row))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>Resolves the slot for <paramref name="col"/>, reaching beyond it per <paramref name="reach"/>.</summary>
        public Vector2Int? Resolve(int col, PlacementReach reach)
        {
            var ownRow = FindFirstReachableEmptyRow(col);
            if (ownRow.HasValue)
            {
                return new Vector2Int(col, ownRow.Value);
            }

            if (reach == PlacementReach.OwnColumn)
            {
                return null;
            }

            if (TryNearestColumn(col, startDistance: 1, _resolveOpenEntry, out var rehome))
            {
                return rehome;
            }

            // Only turn spawns may shove balloons aside; initial fill takes what is genuinely reachable.
            if (reach == PlacementReach.Pressure
                && TryNearestColumn(col, startDistance: 0, _resolvePressureOpen, out var pressured))
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
            var ceilingRow = LowestBlockingRow(col);
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

        // Lowest row holding a non-traversable actor (a bush caps the column here); -1 when unblocked.
        private int LowestBlockingRow(int col)
        {
            for (var row = _grid.Rows - 1; row >= 0; row--)
            {
                if (!_grid.IsEmpty(col, row) && !_grid.IsTraversable(col, row))
                {
                    return row;
                }
            }

            return -1;
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
