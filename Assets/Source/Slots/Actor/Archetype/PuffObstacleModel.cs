using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Traversable by spawn and balance animation paths — balloons arc through freely.
    // No collider; not part of the hit pipeline.
    internal class PuffObstacleModel : IWriteableSlotActor, IPassThrough
    {
        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;
    }
}
