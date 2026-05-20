using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    internal class StaticActorModel : IWriteableSlotActor, IPassThrough
    {
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();

        public SlotActorKind Kind => SlotActorKind.Static;

        IReadOnlyReactiveProperty<Vector2Int> ISlotActor.SlotIndex => SlotIndex;
    }
}
