using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    public interface ISlotActor
    {
        IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
        IReadOnlyReactiveProperty<bool> IsStable { get; }
        SlotActorKind Kind { get; }
    }
}

