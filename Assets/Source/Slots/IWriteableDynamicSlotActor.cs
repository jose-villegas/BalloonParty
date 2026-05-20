using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    public interface IWriteableDynamicSlotActor : IDynamicSlotActor, IWriteableSlotActor
    {
        new ReactiveProperty<Vector2Int> SlotIndex { get; }
        new ReactiveProperty<bool> IsStable { get; }
    }
}
