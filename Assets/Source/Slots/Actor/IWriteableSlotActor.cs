using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    public interface IWriteableSlotActor : ISlotActor
    {
        new Vector2Int SlotIndex { get; set; }
    }
}
