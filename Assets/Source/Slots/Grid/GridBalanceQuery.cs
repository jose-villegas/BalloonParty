using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>Balance heuristics over a <see cref="SlotGrid" />: whether a slot should fall, and where.</summary>
    internal class GridBalanceQuery
    {
        private readonly SlotGrid _grid;

        // Shared scorer so pressure propagation and balance judge moves with the same weights.
        internal MoveWeightEvaluator Evaluator { get; }

        public GridBalanceQuery(SlotGrid grid)
        {
            _grid = grid;
            Evaluator = new MoveWeightEvaluator(grid);
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
            return Evaluator.OptimalBalanceMove(col, row);
        }
    }
}
