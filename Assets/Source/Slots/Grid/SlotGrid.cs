using System;
using System.Collections.Generic;
using BalloonParty.Shared;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Slots.Grid
{
    internal class SlotGrid
    {
        private readonly BalancePathHolder _balancePathHolder;
        private readonly IGameConfiguration _config;
        private readonly Subject<SlotGridChangedEvent> _onChanged = new();
        private readonly IWriteableSlotActor[,] _slots;
        private readonly ISlotActorView[,] _views;

        public IObservable<SlotGridChangedEvent> OnChanged => _onChanged;
        public int Columns => _slots.GetLength(0);
        public int Rows => _slots.GetLength(1);

        public SlotGrid(IGameConfiguration config, BalancePathHolder balancePathHolder)
        {
            _config = config;
            _balancePathHolder = balancePathHolder;
            _slots = new IWriteableSlotActor[config.SlotsSize.x, config.SlotsSize.y];
            _views = new ISlotActorView[config.SlotsSize.x, config.SlotsSize.y];
        }

        public void Place(IWriteableSlotActor actor, ISlotActorView view, Vector2Int index)
        {
            if (_slots[index.x, index.y] != null)
            {
                throw new InvalidOperationException(
                    $"SlotGrid.Place: slot ({index.x},{index.y}) is already occupied.");
            }

            _slots[index.x, index.y] = actor;
            _views[index.x, index.y] = view;
            actor.SlotIndex = index;
            _onChanged.OnNext(new SlotGridChangedEvent(index, SlotGridChangeType.Placed));
        }

        public void Remove(Vector2Int index)
        {
            _slots[index.x, index.y] = null;
            _views[index.x, index.y] = null;
            _onChanged.OnNext(new SlotGridChangedEvent(index, SlotGridChangeType.Removed));
        }

        public IWriteableSlotActor At(Vector2Int index)
        {
            return _slots[index.x, index.y];
        }

        public T ActorAt<T>(Vector2Int index)
            where T : class, IWriteableSlotActor
        {
            return _slots[index.x, index.y] as T;
        }

        public ISlotActorView ViewAt(Vector2Int index)
        {
            return _views[index.x, index.y];
        }

        public T ActorViewAt<T>(Vector2Int index)
            where T : class, ISlotActorView
        {
            return _views[index.x, index.y] as T;
        }

        public bool IsEmpty(int col, int row)
        {
            return col < 0 || col >= Columns
                           || row < 0 || row >= Rows
                           || _slots[col, row] == null;
        }

        public bool IsKind(int col, int row, SlotActorKind kind)
        {
            if (IsEmpty(col, row))
            {
                return false;
            }

            return _slots[col, row].Kind == kind;
        }

        public bool IsTraversable(int col, int row)
        {
            if (IsEmpty(col, row))
            {
                return true;
            }

            return _slots[col, row] is IPassThrough;
        }

        public Vector3[] ComputePath(Vector2Int source, Vector2Int target)
        {
            var colDelta = target.x - source.x;
            var rowDelta = target.y - source.y;
            var steps = Mathf.Max(Mathf.Abs(colDelta), Mathf.Abs(rowDelta));

            if (steps == 0)
            {
                return new[] { IndexToWorldPosition(target) };
            }

            var path = new List<Vector3>(steps + 1);

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var col = Mathf.RoundToInt(Mathf.Lerp(source.x, target.x, t));
                var row = Mathf.RoundToInt(Mathf.Lerp(source.y, target.y, t));

                var inBounds = col >= 0 && col < Columns && row >= 0 && row < Rows;

                if (inBounds && !IsEmpty(col, row)
                              && _slots[col, row].Kind == SlotActorKind.Static
                              && !IsTraversable(col, row))
                {
                    Debug.LogWarning(
                        $"SlotGrid.ComputePath: slot ({col},{row}) is not traversable. " +
                        "Path passes through it — rerouting not yet implemented (Phase 9).");
                }

                if (inBounds && _balancePathHolder.IsInTransit(col, row))
                {
                    Debug.LogWarning(
                        $"SlotGrid.ComputePath: slot ({col},{row}) is in-transit from a balance move. " +
                        "Path crosses a relocating balloon — rerouting not yet implemented (Phase 9).");
                }

                path.Add(IndexToWorldPosition(new Vector2Int(col, row)));
            }

            return path.ToArray();
        }

        public bool IsUnbalanced(int col, int row)
        {
            if (row == 0)
            {
                return false;
            }

            if (IsEmpty(col, row - 1))
            {
                return true;
            }

            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);
            return shiftedCol >= 0 && shiftedCol < Columns && IsEmpty(shiftedCol, row - 1);
        }

        public Vector2Int? OptimalNextEmptySlot(int col, int row)
        {
            if (row <= 0)
            {
                return null;
            }

            var candidates = new[]
            {
                new Vector2Int(col, row - 1),
                new Vector2Int(col + (row % 2 == 0 ? -1 : 1), row - 1)
            };

            var bestWeight = 0;
            var bestIndex = -1;

            for (var k = 0; k < candidates.Length; k++)
            {
                var candidate = candidates[k];
                if (candidate.x < 0 || candidate.x >= Columns)
                {
                    continue;
                }

                if (!IsEmpty(candidate.x, candidate.y))
                {
                    continue;
                }

                var weight = CalculateWeight(candidate.x, candidate.y);
                if (weight >= bestWeight)
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
            {
                for (var row = 0; row < Rows; row++)
                {
                    if (!IsEmpty(col, row))
                    {
                        continue;
                    }

                    yield return new Vector2Int(col, row);
                    break;
                }
            }
        }

        public IEnumerable<Vector2Int> AllEmptySlots()
        {
            for (var col = 0; col < Columns; col++)
            {
                for (var row = 0; row < Rows; row++)
                {
                    if (IsEmpty(col, row))
                    {
                        yield return new Vector2Int(col, row);
                    }
                }
            }
        }

        public static Vector2Int[] HexNeighborIndices(int col, int row)
        {
            var shiftedCol = col + (row % 2 == 0 ? -1 : 1);

            return new[]
            {
                new Vector2Int(col - 1, row),
                new Vector2Int(col + 1, row),
                new Vector2Int(col, row - 1),
                new Vector2Int(shiftedCol, row - 1),
                new Vector2Int(col, row + 1),
                new Vector2Int(shiftedCol, row + 1)
            };
        }

        public List<IWriteableSlotActor> GetNeighbors(int col, int row)
        {
            var neighbors = new List<IWriteableSlotActor>();

            foreach (var idx in HexNeighborIndices(col, row))
            {
                TryAddNeighbor(neighbors, idx.x, idx.y);
            }

            return neighbors;
        }

        public Vector3 IndexToWorldPosition(Vector2Int index)
        {
            return IndexToWorldPosition(index, _config.SlotSeparation, _config.SlotsOffset);
        }

        internal static Vector3 IndexToWorldPosition(Vector2Int index, Vector2 separation, Vector2 offset)
        {
            var hIndex = (index.x * 2) + (index.y % 2);
            return new Vector3(
                ((hIndex - offset.x) * separation.x) - (separation.x / 2f),
                (-index.y * separation.y) + offset.y,
                0f);
        }

        private int CalculateWeight(int col, int row)
        {
            if (row == 0)
            {
                return IsEmpty(col, row) ? 0 : 1;
            }

            var weight = IsEmpty(col, row) ? 0 : 1;
            weight += CalculateWeight(col, row - 1);
            weight += CalculateWeight(col + (row % 2 == 0 ? -1 : 1), row - 1);
            return weight;
        }

        private void TryAddNeighbor(List<IWriteableSlotActor> neighbors, int col, int row)
        {
            if (!IsEmpty(col, row))
            {
                neighbors.Add(_slots[col, row]);
            }
        }
    }
}
