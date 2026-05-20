using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    public interface IDynamicSlotActor : ISlotActor
    {
        new IReadOnlyReactiveProperty<Vector2Int> SlotIndex { get; }
        IReadOnlyReactiveProperty<bool> IsStable { get; }
    }
}
