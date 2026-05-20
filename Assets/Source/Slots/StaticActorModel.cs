using UniRx;
using UnityEngine;

namespace BalloonParty.Slots
{
    // Static actors are inert obstacles — their slot can be visually traversed by
    // spawn and balance animations without requiring a detour.
    internal class StaticActorModel : IWriteableSlotActor, IPassThrough
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

