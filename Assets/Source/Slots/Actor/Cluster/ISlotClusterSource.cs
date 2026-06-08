using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>
    /// Non-generic read-only facade for cluster data. Consumers that need cluster
    /// geometry (e.g. disturbance controllers) inject this without knowing the
    /// underlying model type.
    /// </summary>
    internal interface ISlotClusterSource
    {
        IObservable<SlotClusterChangedEvent> OnClusterChanged { get; }
        IReadOnlyDictionary<int, SlotCluster> Clusters { get; }
        SlotCluster GetClusterForSlot(Vector2Int slot);
        SlotCluster GetClusterAtWorldPosition(Vector3 worldPos);
    }
}
