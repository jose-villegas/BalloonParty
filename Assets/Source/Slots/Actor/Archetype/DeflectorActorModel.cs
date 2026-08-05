using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Slots.Actor.Archetype
{
    // Indestructible; permanently redirects projectiles.
    internal class DeflectorActorModel : IWriteableSlotActor, IHitable, IDeflectsShots
    {
        public Vector2Int SlotIndex { get; private set; }

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex;
            set => SlotIndex = value;
        }

        public SlotActorKind Kind => SlotActorKind.Static;

        // Indestructible, so there is no state that could ever make it stop bouncing.
        public bool DeflectsOrdinaryHit => true;

        public HitOutcome EvaluateHit(DamageContext context)
        {
            return HitOutcome.Deflect;
        }
    }
}
