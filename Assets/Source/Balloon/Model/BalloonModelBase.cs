using BalloonParty.Balloon.Type;
using BalloonParty.Configuration;
using BalloonParty.Nudge;
using BalloonParty.Slots;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    internal abstract class BalloonModelBase : IWriteableBalloonModel, IHasDurability
    {
        public ReactiveProperty<string> Color { get; } = new();
        public BalloonType TypeName { get; init; }
        public ReactiveProperty<int> HitsRemaining { get; } = new(1);
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);

        public NudgeOverride[] NudgeOverrides { get; init; }
        public bool CanHoldItem { get; init; }
        public int ScoreValue { get; init; } = 1;

        public SlotActorKind Kind => SlotActorKind.Dynamic;

        IReadOnlyReactiveProperty<string> IHasColor.Color => Color;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;
        IReadOnlyReactiveProperty<Vector2Int> ISlotActor.SlotIndex => SlotIndex;
        IReadOnlyReactiveProperty<bool> IDynamicSlotActor.IsStable => IsStable;
        IReadOnlyReactiveProperty<ItemType> IBalloonModel.Item => Item;

        public virtual HitOutcome EvaluateHit(int damage)
        {
            var survives = HitsRemaining.Value - damage > 0;
            HitsRemaining.Value -= damage;
            return survives ? HitOutcome.PassThrough : HitOutcome.Pop;
        }
    }
}

