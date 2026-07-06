using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Cluster
{
    /// <summary>Non-generic read-only facade so consumers can access cluster geometry without knowing the model type.</summary>
    internal interface ISlotClusterSource
    {
        IObservable<SlotClusterChangedEvent> OnClusterChanged { get; }
        IReadOnlyDictionary<int, SlotCluster> Clusters { get; }
        SlotCluster GetClusterForSlot(Vector2Int slot);
        SlotCluster GetClusterAtWorldPosition(Vector3 worldPos);
    }
}
