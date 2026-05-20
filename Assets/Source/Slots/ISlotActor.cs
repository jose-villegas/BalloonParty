using UnityEngine;

namespace BalloonParty.Slots
{
    public interface ISlotActor
    {
        Vector2Int SlotIndex { get; }
        SlotActorKind Kind { get; }
    }
}
