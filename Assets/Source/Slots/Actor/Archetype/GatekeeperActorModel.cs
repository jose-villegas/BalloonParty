using BalloonParty.Slots.Capabilities;
using UniRx;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Deflects until HitsRemaining reaches zero, then pops.
    internal class GatekeeperActorModel : IWriteableSlotActor, IHasDurability, IDeflectsShots
    {
        public int MaxHitPoints { get; }
        public ReactiveProperty<int> HitsRemaining { get; }

        int IHasDurability.MaxHitPoints => MaxHitPoints;
        IReadOnlyReactiveProperty<int> IHasDurability.HitsRemaining => HitsRemaining;

        // Mirrors EvaluateHit: it bounces until the hit that takes it to zero, which pops.
        public bool DeflectsOrdinaryHit => HitsRemaining.Value > 1;

        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;

        internal GatekeeperActorModel(int hitsToPop)
        {
            MaxHitPoints = hitsToPop;
            HitsRemaining = new ReactiveProperty<int>(hitsToPop);
        }

        public HitOutcome EvaluateHit(DamageContext context)
        {
            HitsRemaining.Value = System.Math.Max(0, HitsRemaining.Value - context.Damage);
            return HitsRemaining.Value > 0 ? HitOutcome.Deflect : HitOutcome.Pop;
        }
    }
}
