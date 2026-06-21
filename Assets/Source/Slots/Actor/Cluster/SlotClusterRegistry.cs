using System;
using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Maintains clusters of hex-adjacent <typeparamref name="TModel"/> slots.
    /// Subscribes to <see cref="SlotGrid.OnChanged"/> and recomputes adjacency
    /// via flood-fill whenever a matching actor is placed or removed.
    /// When <c>setupOnly</c> is true, the registry builds once at startup and
    /// does not subscribe to grid changes — suitable for static actors that
    /// never change after initial placement.
    /// </summary>
    internal class SlotClusterRegistry<TModel> : IStartable, IDisposable, ISlotClusterSource
        where TModel : class, IClusterableSlotActor
    {
        private readonly SlotGrid _grid;
        private readonly bool _setupOnly;
        private readonly Subject<SlotClusterChangedEvent> _onClusterChanged = new();
        private readonly Dictionary<int, SlotCluster> _clusters = new();
        private readonly Dictionary<Vector2Int, int> _slotToCluster = new();
        private readonly CompositeDisposable _disposables = new();
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        private int _nextClusterId = 1;

        public IObservable<SlotClusterChangedEvent> OnClusterChanged => _onClusterChanged;
        public IReadOnlyDictionary<int, SlotCluster> Clusters => _clusters;

        internal SlotClusterRegistry(SlotGrid grid, bool setupOnly = false)
        {
            _grid = grid;
            _setupOnly = setupOnly;
        }

        public void Start()
        {
            if (!_setupOnly)
            {
                _grid.OnChanged
                    .Subscribe(OnGridChanged)
                    .AddTo(_disposables);
            }

            RebuildAll();
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _onClusterChanged.Dispose();
        }

        public SlotCluster GetClusterForSlot(Vector2Int slot)
        {
            if (_slotToCluster.TryGetValue(slot, out var id))
            {
                return _clusters.GetValueOrDefault(id);
            }

            return null;
        }

        public SlotCluster GetClusterAtWorldPosition(Vector3 worldPos)
        {
            var point = new Vector2(worldPos.x, worldPos.y);
            foreach (var cluster in _clusters.Values)
            {
                if (cluster.WorldBounds.Contains(point))
                {
                    return cluster;
                }
            }

            return null;
        }

        private void OnGridChanged(SlotGridChangedEvent evt)
        {
            var actor = _grid.At(evt.Index);

            if (evt.ChangeType == SlotGridChangeType.Placed)
            {
                if (actor is TModel)
                {
                    OnActorPlaced(evt.Index);
                }
            }
            else if (evt.ChangeType == SlotGridChangeType.Removed)
            {
                if (_slotToCluster.ContainsKey(evt.Index))
                {
                    OnActorRemoved(evt.Index);
                }
            }
        }

        private void OnActorPlaced(Vector2Int slot)
        {
            HexCoordinates.HexNeighborIndices(slot.x, slot.y, _neighborBuffer);

            var neighborClusterIds = new HashSet<int>();
            var firstNeighborId = 0;
            foreach (var neighbor in _neighborBuffer)
            {
                if (_slotToCluster.TryGetValue(neighbor, out var id) && neighborClusterIds.Add(id))
                {
                    firstNeighborId = id;
                }
            }

            switch (neighborClusterIds.Count)
            {
                case 0:
                    CreateSingletonCluster(slot);
                    break;
                case 1:
                    GrowCluster(firstNeighborId, slot);
                    break;
                default:
                    MergeClusters(neighborClusterIds, slot);
                    break;
            }
        }

        private void CreateSingletonCluster(Vector2Int slot)
        {
            var cluster = CreateCluster(new List<Vector2Int> { slot });
            _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                cluster.ClusterId, SlotClusterChangeType.Created, cluster));
        }

        private void GrowCluster(int clusterId, Vector2Int slot)
        {
            var cluster = _clusters[clusterId];
            cluster.AddSlot(slot);
            _slotToCluster[slot] = clusterId;
            RecalculateBounds(cluster);

            _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                cluster.ClusterId, SlotClusterChangeType.Resized, cluster));
        }

        private void MergeClusters(HashSet<int> clusterIds, Vector2Int slot)
        {
            var mergedSlots = new List<Vector2Int> { slot };

            foreach (var clusterId in clusterIds)
            {
                var oldCluster = _clusters[clusterId];
                mergedSlots.AddRange(oldCluster.Slots);

                foreach (var s in oldCluster.Slots)
                {
                    _slotToCluster.Remove(s);
                }

                _clusters.Remove(clusterId);
                _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                    clusterId, SlotClusterChangeType.Removed, oldCluster));
            }

            var newCluster = CreateCluster(mergedSlots);
            _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                newCluster.ClusterId, SlotClusterChangeType.Created, newCluster));
        }

        private void OnActorRemoved(Vector2Int slot)
        {
            if (!_slotToCluster.TryGetValue(slot, out var clusterId))
            {
                return;
            }

            var oldCluster = _clusters[clusterId];
            var remainingSlots = new List<Vector2Int>();
            foreach (var s in oldCluster.Slots)
            {
                if (s != slot)
                {
                    remainingSlots.Add(s);
                }
            }

            foreach (var s in oldCluster.Slots)
            {
                _slotToCluster.Remove(s);
            }

            _clusters.Remove(clusterId);
            _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                clusterId, SlotClusterChangeType.Removed, oldCluster));

            if (remainingSlots.Count == 0)
            {
                return;
            }

            var visited = new HashSet<Vector2Int>();
            foreach (var remaining in remainingSlots)
            {
                if (visited.Contains(remaining))
                {
                    continue;
                }

                var component = FloodFill(remaining, remainingSlots, visited);
                var newCluster = CreateCluster(component);
                _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                    newCluster.ClusterId, SlotClusterChangeType.Created, newCluster));
            }
        }

        private void RebuildAll()
        {
            _clusters.Clear();
            _slotToCluster.Clear();

            var allSlots = new List<Vector2Int>();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var idx = new Vector2Int(col, row);
                    if (_grid.At(idx) is TModel)
                    {
                        allSlots.Add(idx);
                    }
                }
            }

            if (allSlots.Count == 0)
            {
                return;
            }

            var visited = new HashSet<Vector2Int>();
            foreach (var slot in allSlots)
            {
                if (visited.Contains(slot))
                {
                    continue;
                }

                var component = FloodFill(slot, allSlots, visited);
                var cluster = CreateCluster(component);
                _onClusterChanged.OnNext(new SlotClusterChangedEvent(
                    cluster.ClusterId, SlotClusterChangeType.Created, cluster));
            }
        }

        private List<Vector2Int> FloodFill(
            Vector2Int start,
            ICollection<Vector2Int> validSlots,
            HashSet<Vector2Int> visited)
        {
            var component = new List<Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);
            visited.Add(start);

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                component.Add(current);

                HexCoordinates.HexNeighborIndices(current.x, current.y, _neighborBuffer);
                foreach (var neighbor in _neighborBuffer)
                {
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    if (!validSlots.Contains(neighbor))
                    {
                        continue;
                    }

                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }

            return component;
        }

        private SlotCluster CreateCluster(IReadOnlyList<Vector2Int> slots)
        {
            var id = _nextClusterId++;
            var bounds = ComputeWorldBounds(slots);
            var cluster = new SlotCluster(id, slots, bounds);

            _clusters[id] = cluster;
            foreach (var slot in slots)
            {
                _slotToCluster[slot] = id;
                AssignClusterIdToModel(slot, id);
            }

            return cluster;
        }

        private void RecalculateBounds(SlotCluster cluster)
        {
            cluster.WorldBounds = ComputeWorldBounds(cluster.Slots);
        }

        private void AssignClusterIdToModel(Vector2Int slot, int clusterId)
        {
            if (slot.x >= 0 && slot.x < _grid.Columns && slot.y >= 0 && slot.y < _grid.Rows)
            {
                if (_grid.At(slot) is TModel actor)
                {
                    actor.ClusterId = clusterId;
                }
            }
        }

        private Rect ComputeWorldBounds(IReadOnlyList<Vector2Int> slots)
        {
            if (slots.Count == 0)
            {
                return Rect.zero;
            }

            var first = _grid.IndexToWorldPosition(slots[0]);
            var min = new Vector2(first.x, first.y);
            var max = min;

            for (var i = 1; i < slots.Count; i++)
            {
                var pos = _grid.IndexToWorldPosition(slots[i]);
                min.x = Mathf.Min(min.x, pos.x);
                min.y = Mathf.Min(min.y, pos.y);
                max.x = Mathf.Max(max.x, pos.x);
                max.y = Mathf.Max(max.y, pos.y);
            }

            const float halfSlotPadding = 0.5f;
            min -= Vector2.one * halfSlotPadding;
            max += Vector2.one * halfSlotPadding;

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
