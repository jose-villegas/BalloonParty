using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Blocks a column until destroyed. Deflects until HitsRemaining reaches zero, then pops.
    internal class GatekeeperActorModel : IWriteableSlotActor, IHasDurability
    {
        public ReactiveProperty<int> HitsRemaining { get; }

        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;

        internal GatekeeperActorModel(int hitsToPop)
        {
            HitsRemaining = new ReactiveProperty<int>(hitsToPop);
        }

        public HitOutcome EvaluateHit(DamageContext context)
        {
            HitsRemaining.Value = System.Math.Max(0, HitsRemaining.Value - context.Damage);
            return HitsRemaining.Value > 0 ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}
