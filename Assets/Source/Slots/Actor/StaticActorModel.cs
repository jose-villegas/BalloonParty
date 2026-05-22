using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    internal class StaticActorModel : IWriteableSlotActor, IPassThrough
    {
        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;

        internal StaticActorModel(Vector2Int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
}
