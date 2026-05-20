using System.Collections.Generic;
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
        public BalloonType TypeName { get; }
        public ReactiveProperty<int> HitsRemaining { get; }
        public Vector2Int SlotIndex { get; set; }
        public ReactiveProperty<bool> IsStable { get; } = new(true);
        public ReactiveProperty<ItemType> Item { get; } = new(ItemType.None);

        public IReadOnlyList<NudgeOverride> NudgeOverrides { get; }
        public bool CanHoldItem { get; }
        public int ScoreValue { get; }

        public SlotActorKind Kind => SlotActorKind.Dynamic;

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;
        IReadOnlyReactiveProperty<bool> IDynamicSlotActor.IsStable => IsStable;
        IReadOnlyReactiveProperty<ItemType> IBalloonModel.Item => Item;

        protected BalloonModelBase(BalloonModelConfig config)
        {
            TypeName = config.TypeName;
            ScoreValue = config.ScoreValue;
            CanHoldItem = config.CanHoldItem;
            NudgeOverrides = config.NudgeOverrides;
            HitsRemaining = new ReactiveProperty<int>(config.HitsToPop);
        }

        public virtual HitOutcome EvaluateHit(int damage)
        {
            var survives = HitsRemaining.Value - damage > 0;
            HitsRemaining.Value -= damage;
            return survives ? HitOutcome.PassThrough : HitOutcome.Pop;
        }
    }
}
