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

        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

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
            var path = new List<Vector3>();
            ComputePath(source, target, path);
            return path.ToArray();
        }

        public void ComputePath(Vector2Int source, Vector2Int target, List<Vector3> results)
        {
            results.Clear();

            var colDelta = target.x - source.x;
            var rowDelta = target.y - source.y;
            var steps = Mathf.Max(Mathf.Abs(colDelta), Mathf.Abs(rowDelta));

            if (steps == 0)
            {
                results.Add(IndexToWorldPosition(target));
                return;
            }

            for (var i = 0; i <= steps; i++)
            {
                var t = (float)i / steps;
                var col = Mathf.RoundToInt(Mathf.Lerp(source.x, target.x, t));
                var row = Mathf.RoundToInt(Mathf.Lerp(source.y, target.y, t));

                WarnIfPathBlocked(col, row);
                results.Add(IndexToWorldPosition(new Vector2Int(col, row)));
            }
        }

        // Diagnostics only: the path is laid straight through; rerouting around blockers/relocating
        // balloons isn't implemented yet (Phase 9), so warn when the line crosses one.
        private void WarnIfPathBlocked(int col, int row)
        {
            var inBounds = col >= 0 && col < Columns && row >= 0 && row < Rows;
            if (!inBounds)
            {
                return;
            }

            if (!IsEmpty(col, row)
                && _slots[col, row].Kind == SlotActorKind.Static
                && !IsTraversable(col, row))
            {
                Debug.LogWarning(
                    $"SlotGrid.ComputePath: slot ({col},{row}) is not traversable. " +
                    "Path passes through it — rerouting not yet implemented (Phase 9).");
            }

            if (_balancePathHolder.IsInTransit(col, row))
            {
                Debug.LogWarning(
                    $"SlotGrid.ComputePath: slot ({col},{row}) is in-transit from a balance move. " +
                    "Path crosses a relocating balloon — rerouting not yet implemented (Phase 9).");
            }
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

        public List<IWriteableSlotActor> GetNeighbors(int col, int row)
        {
            var neighbors = new List<IWriteableSlotActor>();
            GetNeighbors(col, row, neighbors);
            return neighbors;
        }

        public void GetNeighbors(int col, int row, List<IWriteableSlotActor> results)
        {
            results.Clear();
            HexCoordinates.HexNeighborIndices(col, row, _neighborBuffer);
            foreach (var idx in _neighborBuffer)
            {
                TryAddNeighbor(results, idx.x, idx.y);
            }
        }

        public Vector3 IndexToWorldPosition(Vector2Int index)
        {
            return HexCoordinates.IndexToWorldPosition(index, _config.SlotSeparation, _config.SlotsOffset);
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
