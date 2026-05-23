using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Blocks spawn and balance animation paths — balloons must route around this slot.
    // No collider; not part of the hit pipeline. Projectiles pass through unaffected.
    internal class BushObstacleModel : IWriteableSlotActor
    {
        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;

        internal BushObstacleModel() { }
    }
}
