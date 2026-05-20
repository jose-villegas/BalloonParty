using UnityEngine;

namespace BalloonParty.Slots
{
    public interface IWriteableSlotActor : ISlotActor
    {
        new Vector2Int SlotIndex { get; set; }
    }
}
