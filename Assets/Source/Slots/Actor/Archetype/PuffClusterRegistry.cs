using System;
using System.Collections.Generic;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Maintains clusters of hex-adjacent <see cref="PuffObstacleModel"/> slots.
    /// Subscribes to <see cref="SlotGrid.OnChanged"/> and recomputes adjacency
    /// via flood-fill whenever a Puff is placed or removed.
    /// </summary>
    internal class PuffClusterRegistry : IStartable, IDisposable
    {
        private readonly SlotGrid _grid;
        private readonly Subject<PuffClusterChangedEvent> _onClusterChanged = new();
        private readonly Dictionary<int, PuffCluster> _clusters = new();
        private readonly Dictionary<Vector2Int, int> _slotToCluster = new();
        private readonly CompositeDisposable _disposables = new();
        private readonly Vector2Int[] _neighborBuffer = new Vector2Int[6];

        private int _nextClusterId = 1;

        internal IObservable<PuffClusterChangedEvent> OnClusterChanged => _onClusterChanged;
        internal IReadOnlyDictionary<int, PuffCluster> Clusters => _clusters;

        [Inject]
        internal PuffClusterRegistry(SlotGrid grid)
        {
            _grid = grid;
        }

        public void Start()
        {
            _grid.OnChanged
                .Subscribe(OnGridChanged)
                .AddTo(_disposables);

            RebuildAll();
        }

        public void Dispose()
        {
            _disposables.Dispose();
            _onClusterChanged.Dispose();
        }

        /// <summary>
        /// Returns the cluster that contains the given slot, or null if the slot
        /// is not part of any Puff cluster.
        /// </summary>
        internal PuffCluster GetClusterForSlot(Vector2Int slot)
        {
            if (_slotToCluster.TryGetValue(slot, out var id))
            {
                return _clusters.GetValueOrDefault(id);
            }

            return null;
        }

        /// <summary>
        /// Checks whether a world-space position overlaps any cluster bounds.
        /// Returns the first matching cluster, or null.
        /// </summary>
        internal PuffCluster GetClusterAtWorldPosition(Vector3 worldPos)
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
                if (actor is PuffObstacleModel)
                {
                    OnPuffPlaced(evt.Index);
                }
            }
            else if (evt.ChangeType == SlotGridChangeType.Removed)
            {
                if (_slotToCluster.ContainsKey(evt.Index))
                {
                    OnPuffRemoved(evt.Index);
                }
            }
        }

        private void OnPuffPlaced(Vector2Int slot)
        {
            var neighborClusterIds = new HashSet<int>();
            SlotGrid.HexNeighborIndices(slot.x, slot.y, _neighborBuffer);

            foreach (var neighbor in _neighborBuffer)
            {
                if (_slotToCluster.TryGetValue(neighbor, out var neighborClusterId))
                {
                    neighborClusterIds.Add(neighborClusterId);
                }
            }

            if (neighborClusterIds.Count == 0)
            {
                // No adjacent Puff clusters — create a new single-slot cluster
                var cluster = CreateCluster(new List<Vector2Int> { slot });
                _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                    cluster.ClusterId, PuffClusterChangeType.Created, cluster));
            }
            else if (neighborClusterIds.Count == 1)
            {
                // Merges into one existing cluster
                var enumerator = neighborClusterIds.GetEnumerator();
                enumerator.MoveNext();
                var existingId = enumerator.Current;
                enumerator.Dispose();

                var cluster = _clusters[existingId];
                cluster.AddSlot(slot);
                _slotToCluster[slot] = existingId;
                RecalculateBounds(cluster);

                _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                    cluster.ClusterId, PuffClusterChangeType.Resized, cluster));
            }
            else
            {
                // Bridges multiple clusters — merge them all
                var mergedSlots = new List<Vector2Int> { slot };

                foreach (var clusterId in neighborClusterIds)
                {
                    var oldCluster = _clusters[clusterId];
                    mergedSlots.AddRange(oldCluster.Slots);

                    // Remove the old cluster
                    foreach (var s in oldCluster.Slots)
                    {
                        _slotToCluster.Remove(s);
                    }

                    _clusters.Remove(clusterId);
                    _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                        clusterId, PuffClusterChangeType.Removed, oldCluster));
                }

                var newCluster = CreateCluster(mergedSlots);
                _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                    newCluster.ClusterId, PuffClusterChangeType.Created, newCluster));
            }
        }

        private void OnPuffRemoved(Vector2Int slot)
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

            // Remove the old cluster entirely
            foreach (var s in oldCluster.Slots)
            {
                _slotToCluster.Remove(s);
            }

            _clusters.Remove(clusterId);
            _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                clusterId, PuffClusterChangeType.Removed, oldCluster));

            if (remainingSlots.Count == 0)
            {
                return;
            }

            // Flood-fill remaining slots — may produce multiple new clusters (split)
            var visited = new HashSet<Vector2Int>();
            foreach (var remaining in remainingSlots)
            {
                if (visited.Contains(remaining))
                {
                    continue;
                }

                var component = FloodFill(remaining, remainingSlots, visited);
                var newCluster = CreateCluster(component);
                _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                    newCluster.ClusterId, PuffClusterChangeType.Created, newCluster));
            }
        }

        private void RebuildAll()
        {
            _clusters.Clear();
            _slotToCluster.Clear();

            var allPuffs = new List<Vector2Int>();
            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var idx = new Vector2Int(col, row);
                    if (_grid.At(idx) is PuffObstacleModel)
                    {
                        allPuffs.Add(idx);
                    }
                }
            }

            if (allPuffs.Count == 0)
            {
                return;
            }

            var visited = new HashSet<Vector2Int>();
            foreach (var puff in allPuffs)
            {
                if (visited.Contains(puff))
                {
                    continue;
                }

                var component = FloodFill(puff, allPuffs, visited);
                var cluster = CreateCluster(component);
                _onClusterChanged.OnNext(new PuffClusterChangedEvent(
                    cluster.ClusterId, PuffClusterChangeType.Created, cluster));
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

                SlotGrid.HexNeighborIndices(current.x, current.y, _neighborBuffer);
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

        private PuffCluster CreateCluster(List<Vector2Int> slots)
        {
            var id = _nextClusterId++;
            var bounds = ComputeWorldBounds(slots);
            var cluster = new PuffCluster(id, slots, bounds);

            _clusters[id] = cluster;
            foreach (var slot in slots)
            {
                _slotToCluster[slot] = id;
                AssignClusterIdToModel(slot, id);
            }

            return cluster;
        }

        private void RecalculateBounds(PuffCluster cluster)
        {
            cluster.WorldBounds = ComputeWorldBounds(cluster.Slots);
        }

        private void AssignClusterIdToModel(Vector2Int slot, int clusterId)
        {
            if (slot.x >= 0 && slot.x < _grid.Columns && slot.y >= 0 && slot.y < _grid.Rows)
            {
                var actor = _grid.At(slot) as PuffObstacleModel;
                if (actor != null)
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

            // Expand by half-slot separation on each side
            const float halfSlotPadding = 0.5f;
            min -= Vector2.one * halfSlotPadding;
            max += Vector2.one * halfSlotPadding;

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
