using System;
using System.Collections.Generic;
using BalloonParty.Balloon.Model;
using UniRx;
using UnityEngine;
using Random = UnityEngine.Random;

namespace BalloonParty.Slots
{
    public class SlotGrid
    {
        private readonly IGameConfiguration _config;
        private readonly Subject<SlotGridChangedEvent> _onChanged = new();
        private readonly BalloonModel[,] _slots;

        public SlotGrid(IGameConfiguration config)
        {
            _config = config;
            _slots = new BalloonModel[config.SlotsSize.x, config.SlotsSize.y];
        }

        public IObservable<SlotGridChangedEvent> OnChanged => _onChanged;
        public int Columns => _slots.GetLength(0);
        public int Rows => _slots.GetLength(1);

        public string RandomColorName()
        {
            return _config.BalloonColors[Random.Range(0, _config.BalloonColors.Length)].Name;
        }

        public void Place(BalloonModel balloon, Vector2Int index)
        {
            _slots[index.x, index.y] = balloon;
            balloon.SlotIndex.Value = index;
            _onChanged.OnNext(new SlotGridChangedEvent(index, SlotGridChangeType.Placed));
        }

        public void Remove(Vector2Int index)
        {
            _slots[index.x, index.y] = null;
            _onChanged.OnNext(new SlotGridChangedEvent(index, SlotGridChangeType.Removed));
        }

        public BalloonModel At(Vector2Int index)
        {
            return _slots[index.x, index.y];
        }

        public bool IsEmpty(int col, int row)
        {
            return col < 0 || col >= Columns
                           || row < 0 || row >= Rows
                           || _slots[col, row] == null;
        }

        // A slot is unbalanced when either of the two slots directly above it is empty.
        public bool IsUnbalanced(int col, int row)
        {
            if (row == 0) return false;

            if (IsEmpty(col, row - 1)) return true;

            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);
            return shiftedCol >= 0 && shiftedCol < Columns && IsEmpty(shiftedCol, row - 1);
        }

        public Vector2Int? OptimalNextEmptySlot(int col, int row)
        {
            if (row <= 0) return null;

            var candidates = new[]
            {
                new Vector2Int(col, row - 1),
                new Vector2Int(col + (row % 2 == 0 ? -1 : 1), row - 1)
            };

            var bestWeight = -1;
            var bestIndex = -1;

            for (var k = 0; k < candidates.Length; k++)
            {
                var candidate = candidates[k];
                if (candidate.x < 0 || candidate.x >= Columns) continue;
                if (!IsEmpty(candidate.x, candidate.y)) continue;

                var weight = CalculateWeight(candidate.x, candidate.y);
                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestIndex = k;
                }
            }

            return bestIndex >= 0 ? candidates[bestIndex] : null;
        }

        public IEnumerable<Vector2Int> BottomEmptySlotPerColumn()
        {
            for (var col = 0; col < Columns; col++)
            for (var row = 0; row < Rows; row++)
            {
                if (!IsEmpty(col, row)) continue;
                yield return new Vector2Int(col, row);
                break;
            }
        }

        public List<BalloonModel> GetNeighbors(int col, int row)
        {
            var neighbors = new List<BalloonModel>();
            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);

            TryAddNeighbor(neighbors, col - 1, row);
            TryAddNeighbor(neighbors, col + 1, row);
            TryAddNeighbor(neighbors, col, row - 1);
            TryAddNeighbor(neighbors, shiftedCol, row - 1);
            TryAddNeighbor(neighbors, col, row + 1);
            TryAddNeighbor(neighbors, shiftedCol, row + 1);

            return neighbors;
        }

        public bool AllBalloonsStable()
        {
            for (var col = 0; col < Columns; col++)
            for (var row = 0; row < Rows; row++)
                if (_slots[col, row] != null && !_slots[col, row].IsStable.Value)
                    return false;
            return true;
        }

        public Vector3 IndexToWorldPosition(Vector2Int index)
        {
            var hIndex = index.x * 2 + index.y % 2;
            return new Vector3(
                (hIndex - _config.SlotsOffset.x) * _config.SlotSeparation.x - _config.SlotSeparation.x / 2f,
                -index.y * _config.SlotSeparation.y + _config.SlotsOffset.y,
                0f);
        }

        private int CalculateWeight(int col, int row)
        {
            if (row == 0) return IsEmpty(col, row) ? 0 : 1;

            var weight = IsEmpty(col, row) ? 0 : 1;
            weight += CalculateWeight(col, row - 1);
            weight += CalculateWeight(col + (row % 2 == 0 ? -1 : 1), row - 1);
            return weight;
        }

        private void TryAddNeighbor(List<BalloonModel> neighbors, int col, int row)
        {
            if (!IsEmpty(col, row))
                neighbors.Add(_slots[col, row]);
        }
    }
}