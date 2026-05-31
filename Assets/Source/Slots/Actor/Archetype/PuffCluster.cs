using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    /// <summary>
    /// Represents a group of hex-adjacent <see cref="PuffObstacleModel"/> slots
    /// that share a single cloud visual.
    /// </summary>
    internal class PuffCluster
    {
        private readonly List<Vector2Int> _slots = new();

        public int ClusterId { get; }
        public IReadOnlyList<Vector2Int> Slots => _slots;
        public Rect WorldBounds { get; internal set; }

        internal PuffCluster(int clusterId)
        {
            ClusterId = clusterId;
        }

        internal PuffCluster(int clusterId, IEnumerable<Vector2Int> slots, Rect worldBounds)
        {
            ClusterId = clusterId;
            _slots.AddRange(slots);
            WorldBounds = worldBounds;
        }

        internal void SetSlots(IEnumerable<Vector2Int> slots, Rect worldBounds)
        {
            _slots.Clear();
            _slots.AddRange(slots);
            WorldBounds = worldBounds;
        }

        internal void AddSlot(Vector2Int slot)
        {
            if (!_slots.Contains(slot))
            {
                _slots.Add(slot);
            }
        }
    }
}

