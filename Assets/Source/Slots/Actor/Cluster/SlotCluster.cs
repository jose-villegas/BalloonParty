using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// A group of hex-adjacent <see cref="IClusterableSlotActor"/> slots that share
    /// a single merged visual (cloud, bush, etc.).
    /// </summary>
    internal class SlotCluster
    {
        private readonly List<Vector2Int> _slots = new();

        public int ClusterId { get; }
        public IReadOnlyList<Vector2Int> Slots => _slots;
        public Rect WorldBounds { get; internal set; }

        internal SlotCluster(int clusterId)
        {
            ClusterId = clusterId;
        }

        internal SlotCluster(int clusterId, IEnumerable<Vector2Int> slots, Rect worldBounds)
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
            _slots.Add(slot);
        }
    }
}
