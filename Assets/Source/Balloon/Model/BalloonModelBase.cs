using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    internal abstract class BalloonModelBase : IWriteableBalloonModel, IPressureMovable, IBalanceInfluence,
        IHasDeflectStamp
    {
        public BalloonType TypeName { get; }
        public int RegistryHandle { get; set; } = -1;
        public int MaxBalanceSteps { get; }
        public int BalancePriority { get; }
        public bool DirectBalanceMotion { get; }
        public bool OmnidirectionalBalance { get; }
        public float DeflectStampScale { get; }
        public ReactiveProperty<int> HitsRemaining { get; }
        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);

        public abstract IReadOnlyList<NudgeOverride> NudgeOverrides { get; }

        public SlotActorKind Kind => SlotActorKind.Dynamic;

        // Default: a shoved balloon steps one cell to a neighbour.
        public virtual PressureResponse PushResponse => PressureResponse.ShoveNeighbour;

        // Default non-fatal hit outcome; the fatal (Pop) branch stays in EvaluateNormalHit.
        protected virtual HitOutcome SurviveOutcome => HitOutcome.PassThrough;

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
            MaxBalanceSteps = config.MaxBalanceSteps;
            BalancePriority = config.BalancePriority;
            DirectBalanceMotion = config.DirectBalanceMotion;
            OmnidirectionalBalance = config.OmnidirectionalBalance;
            DeflectStampScale = config.DeflectStampScale;
        }

        // Subclasses override to apply their own bias strategy using config.BalanceBias.
        public virtual int WeightBias(SlotGrid grid, Vector2Int candidate)
        {
            return 0;
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

        protected HitOutcome EvaluateNormalHit(DamageContext context)
        {
            var survives = HitsRemaining.Value - context.Damage > 0;
            HitsRemaining.Value -= context.Damage;
            return survives ? SurviveOutcome : HitOutcome.Pop;
        }
    }
}
