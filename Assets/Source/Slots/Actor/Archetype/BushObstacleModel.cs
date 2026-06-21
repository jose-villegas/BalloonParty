using BalloonParty.Slots.Actor.Cluster;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Blocks spawn and balance animation paths — balloons must route around this slot.
    // No collider; not part of the hit pipeline. Projectiles fly over unaffected.
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
