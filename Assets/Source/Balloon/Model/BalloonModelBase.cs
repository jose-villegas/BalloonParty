using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    internal abstract class BalloonModelBase : IWriteableBalloonModel
    {
        public BalloonType TypeName { get; }
        public ReactiveProperty<int> HitsRemaining { get; }
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);

        // Subclasses own NudgeOverrides — not all balloons are nudgeable in the same way.
        public abstract IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        public SlotActorKind Kind => SlotActorKind.Dynamic;

        IReadOnlyReactiveProperty<bool> IDynamicSlotActor.IsStable => IsStable;
        IReadOnlyReactiveProperty<Vector2Int> IDynamicSlotActor.SlotIndex => SlotIndex;

        Vector2Int ISlotActor.SlotIndex => SlotIndex.Value;

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex.Value;
            set => SlotIndex.Value = value;
        }

        protected BalloonModelBase(BalloonModelConfig config)
        {
            TypeName = config.TypeName;
            HitsRemaining = new ReactiveProperty<int>(config.HitsToPop);
        }

        public virtual HitOutcome EvaluateHit(DamageContext context)
        {
            if (context.Flags.HasFlag(DamageFlags.Piercing))
            {
                HitsRemaining.Value = 0;
                return HitOutcome.Pop;
            }

            return EvaluateNormalHit(context);
        }

        protected virtual HitOutcome EvaluateNormalHit(DamageContext context)
        {
            var survives = HitsRemaining.Value - context.Damage > 0;
            HitsRemaining.Value -= context.Damage;
            return survives ? HitOutcome.PassThrough : HitOutcome.Pop;
        }
    }
}

