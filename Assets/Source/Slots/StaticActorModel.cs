using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    internal class StaticActorModel : IWriteableSlotActor
    {
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();

        // Satisfies IWriteableSlotActor until Phase 7 introduces IDynamicSlotActor.
        // IsStable carries no semantic meaning on a static actor.
        public ReactiveProperty<bool> IsStable { get; } = new(true);

        public SlotActorKind Kind => SlotActorKind.Static;

        IReadOnlyReactiveProperty<Vector2Int> ISlotActor.SlotIndex => SlotIndex;
        IReadOnlyReactiveProperty<bool> ISlotActor.IsStable => IsStable;
    }
}

