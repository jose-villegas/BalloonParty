using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    public interface ISlotActor
    {
        Vector2Int SlotIndex { get; }
        SlotActorKind Kind { get; }
    }
}
