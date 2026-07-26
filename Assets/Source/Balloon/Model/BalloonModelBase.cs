using System.Collections.Generic;
using BalloonParty.Balloon.Type;
using BalloonParty.Nudge;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Capabilities;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;

namespace BalloonParty.Balloon.Model
{
    internal abstract class BalloonModelBase :
        IWriteableBalloonModel, IPressureMovable, IBalanceInfluence, IBalanceBiasSource
    {
        public BalloonType TypeName { get; }
        public int RegistryHandle { get; set; } = -1;
        public int MaxBalanceSteps { get; }
        public float MoveSpeed { get; }
        public int BalancePriority { get; }
        public bool DirectBalanceMotion { get; }
        public bool OmnidirectionalBalance { get; }
        public int MaxHitPoints { get; }
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

        // IBalanceBiasSource: the read-set the shared bias formulas (Shared/Extensions/BalanceBiasExtensions)
        // need from a neighbour. BiasTypeId is TypeName's ordinal, not the enum — Slots must not reference
        // Balloon.Type.
        string IBalanceBiasSource.ColorId => this.GetColorId();
        int IBalanceBiasSource.BiasTypeId => (int)TypeName;

        // Overridden per subclass with its config-authored bias kind/value; None/0 (this default) is
        // exactly Unbreakable's pre-Phase-B WeightBias behavior — Evaluate(None, ...) is always 0.
        public virtual BalanceBiasKind BiasKind => BalanceBiasKind.None;
        public virtual float BiasValue => 0f;

        Vector2Int ISlotActor.SlotIndex => SlotIndex.Value;

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex.Value;
            set => SlotIndex.Value = value;
        }

        protected BalloonModelBase(BalloonModelConfig config)
        {
            TypeName = config.TypeName;
            MaxHitPoints = config.HitsToPop;
            HitsRemaining = new ReactiveProperty<int>(config.HitsToPop);
            MaxBalanceSteps = config.MaxBalanceSteps;
            MoveSpeed = config.MoveSpeed;
            BalancePriority = config.BalancePriority;
            DirectBalanceMotion = config.DirectBalanceMotion;
            OmnidirectionalBalance = config.OmnidirectionalBalance;
        }

        // Shared across every subclass: BiasKind picks the formula, BiasValue its magnitude — a
        // subclass opts in purely by overriding those two properties (Evaluate(None, ...) is always 0).
        public int WeightBias(SlotGrid grid, Vector2Int candidate)
        {
            return this.Evaluate(BiasKind, grid, candidate, BiasValue);
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
