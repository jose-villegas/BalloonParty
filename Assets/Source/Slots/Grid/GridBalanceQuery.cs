using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>Balance heuristics over a <see cref="SlotGrid" />: whether a slot should fall, and where.</summary>
    internal class GridBalanceQuery
    {
        private readonly SlotGrid _grid;
        private readonly Dictionary<int, int> _weightMemo = new();

        public GridBalanceQuery(SlotGrid grid)
        {
            _grid = grid;
        }

        public bool IsUnbalanced(int col, int row)
        {
            if (row == 0)
            {
                return false;
            }

            if (_grid.IsEmpty(col, row - 1))
            {
                return true;
            }

            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);
            return shiftedCol >= 0 && shiftedCol < _grid.Columns && _grid.IsEmpty(shiftedCol, row - 1);
        }

        public Vector2Int? OptimalNextEmptySlot(int col, int row)
        {
            if (row <= 0)
            {
                return null;
            }

            // Memo is only valid for the current grid state.
            _weightMemo.Clear();

            // The moving actor can inject its own per-candidate offset (e.g. a same-type separation tendency).
            var influence = _grid.At(new Vector2Int(col, row)) as IBalanceInfluence;

            var bestWeight = int.MinValue;
            var bestCol = -1;
            var candidateShift = row % 2 == 0 ? -1 : 1;
            var targetRow = row - 1;

            if (_grid.IsEmpty(col, targetRow))
            {
                bestWeight = TotalWeight(col, targetRow, influence);
                bestCol = col;
            }

            var shiftedCol = col + candidateShift;
            if (shiftedCol >= 0 && shiftedCol < _grid.Columns && _grid.IsEmpty(shiftedCol, targetRow))
            {
                // >= keeps the historical tie-break: the parity-shifted slot wins equal scores.
                if (TotalWeight(shiftedCol, targetRow, influence) >= bestWeight)
                {
                    bestCol = shiftedCol;
                }
            }

            return bestCol >= 0 ? new Vector2Int(bestCol, targetRow) : null;
        }

        private int TotalWeight(int col, int row, IBalanceInfluence influence)
        {
            var weight = CalculateWeight(col, row);
            if (influence != null)
            {
                weight += influence.WeightBias(_grid, new Vector2Int(col, row));
            }

            return weight;
        }

        private int CalculateWeight(int col, int row)
        {
            var key = col * _grid.Rows + row;
            if (_weightMemo.TryGetValue(key, out var cached))
            {
                return cached;
            }

            int weight;
            if (row == 0)
            {
                weight = _grid.IsEmpty(col, row) ? 0 : 1;
            }
            else
            {
                weight = _grid.IsEmpty(col, row) ? 0 : 1;
                weight += CalculateWeight(col, row - 1);
                weight += CalculateWeight(col + (row % 2 == 0 ? -1 : 1), row - 1);
            }

            _weightMemo[key] = weight;
            return weight;
        }
    }
}
