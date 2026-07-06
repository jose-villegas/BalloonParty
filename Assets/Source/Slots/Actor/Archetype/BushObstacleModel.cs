using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Blocks spawn/balance paths; no collider, so projectiles pass over unaffected.
    internal class BushObstacleModel : IClusterableSlotActor, IGridActorModel
    {
        public Vector2Int SlotIndex { get; private set; }
        public int ClusterId { get; set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public GridActorType ActorType => GridActorType.Bush;
        public SlotActorKind Kind => SlotActorKind.Static;
    }
}
