using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    public interface ISlotActor
    {
        IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
        SlotActorKind Kind { get; }
    }
}

