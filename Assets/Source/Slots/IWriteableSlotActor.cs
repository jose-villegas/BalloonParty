using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    public interface IWriteableSlotActor : ISlotActor
    {
        new ReactiveProperty<Vector2Int> SlotIndex { get; }
    }
}

