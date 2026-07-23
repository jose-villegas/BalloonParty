using System.Collections.Generic;
using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    /// <summary>
    ///     Scores hex-neighbour moves with one weight function: support-cone buoyancy for the two up
    ///     neighbours (always candidates), plus the actor's <see cref="IBalanceInfluence" /> bias, plus a
    ///     dominant pressure term under an active <see cref="ShoveVector" /> — which alone unlocks side
    ///     and down moves.
    /// </summary>
    internal class MoveWeightEvaluator
    {
        // Orders of magnitude above any support+bias total (support caps at ~2×Rows, biases are small
        // ints), so an aligned shove candidate always beats every buoyancy-only one.
        internal const int PressureGain = 100000;

        private readonly SlotGrid _grid;
        private readonly Dictionary<int, int> _weightMemo = new();
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        // Grid version the memo was built at; -1 forces a rebuild on first use.
        private int _memoVersion = -1;

        public MoveWeightEvaluator(SlotGrid grid)
        {
            _grid = grid;
        }

        /// <summary>The classic up-only balance move: best of the two up neighbours, or null.</summary>
        public Vector2Int? OptimalBalanceMove(int col, int row)
        {
            return BestMove(col, row, ShoveVector.None);
        }

        /// <summary>Best-scoring empty neighbour of (col,row) under an optional shove, or null.</summary>
        public Vector2Int? BestMove(int col, int row, in ShoveVector shove)
        {
            EnsureMemoFresh();

            var from = new Vector2Int(col, row);
            var influence = InfluenceAt(from);

            HexCoordinates.HexNeighborIndices(col, row, _neighborBuffer);

            var bestWeight = int.MinValue;
            var bestSlot = new Vector2Int(-1, -1);

            // Buffer order puts straight-up before parity-shifted; >= keeps the historical tie-break
            // (the shifted slot wins equal scores).
            foreach (var candidate in _neighborBuffer)
            {
                if (ScoreMove(from, candidate, influence, shove, out var weight) && weight >= bestWeight)
                {
                    bestWeight = weight;
                    bestSlot = candidate;
                }
            }

            return bestSlot.x >= 0 ? bestSlot : (Vector2Int?)null;
        }

        /// <summary>Scores a single hex-neighbour move; false when <paramref name="to" /> is no candidate.</summary>
        public bool TryScoreMove(Vector2Int from, Vector2Int to, in ShoveVector shove, out int weight)
        {
            EnsureMemoFresh();
            return ScoreMove(from, to, InfluenceAt(from), shove, out weight);
        }

        // CalculateWeight is a pure function of grid occupancy, so the memo stays valid until the grid
        // mutates. A balance sweep evaluates BestMove/TryScoreMove O(passes × candidates) times over
        // identical state between moves; gating the clear on SlotGrid.MutationVersion (bumped only at
        // Place/Remove) lets the memo persist across those calls instead of rebuilding every time,
        // while still invalidating exactly — and only — when occupancy changes.
        private void EnsureMemoFresh()
        {
            if (_memoVersion == _grid.MutationVersion)
            {
                return;
            }

            _weightMemo.Clear();
            _memoVersion = _grid.MutationVersion;
        }

        private bool ScoreMove(
            Vector2Int from, Vector2Int to, IBalanceInfluence influence, in ShoveVector shove, out int weight)
        {
            weight = 0;
            if (!_grid.InBounds(to) || !_grid.IsEmpty(to.x, to.y))
            {
                return false;
            }

            var isUpMove = to.y == from.y - 1;
            var omnidirectional = influence is { OmnidirectionalBalance: true };
            var alignment = 0f;
            if (shove.Active)
            {
                var delta = _grid.IndexToWorldPosition(to) - _grid.IndexToWorldPosition(from);
                alignment = Vector2.Dot(((Vector2)delta).normalized, shove.Direction);
            }

            if (isUpMove)
            {
                weight = CalculateWeight(to.x, to.y);
            }
            else if (!omnidirectional && (!shove.Active || alignment < 0f))
            {
                // Side/down moves exist only under a shove or for omnidirectional actors,
                // and never back into the shover.
                return false;
            }

            if (shove.Active)
            {
                weight += Mathf.RoundToInt(PressureGain * Mathf.Max(0f, alignment));
            }

            if (influence != null)
            {
                weight += influence.WeightBias(_grid, to);
            }

            return true;
        }

        private IBalanceInfluence InfluenceAt(Vector2Int slot)
        {
            // At() is not bounds-safe; an out-of-bounds mover simply carries no bias.
            return _grid.InBounds(slot) ? _grid.At(slot) as IBalanceInfluence : null;
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
