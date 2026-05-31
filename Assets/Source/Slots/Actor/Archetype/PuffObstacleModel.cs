using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    internal class PuffObstacleModel : IWriteableSlotActor, IPassThrough
    {
        public Vector2Int SlotIndex { get; private set; }
        public int ClusterId { get; internal set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;
    }
}
