using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Indestructible; absorbs the projectile, ending the turn.
    internal class AbsorberActorModel : IWriteableSlotActor, IHitable
    {
        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;

        public HitOutcome EvaluateHit(DamageContext context)
        {
            return HitOutcome.Absorb;
        }
    }
}
