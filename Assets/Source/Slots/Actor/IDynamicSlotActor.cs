using UniRx;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    public interface IDynamicSlotActor : ISlotActor
    {
        new IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
        IReadOnlyReactiveProperty<bool> IsStable { get; }
    }
}
