using BalloonParty.Slots.Actor;
using UnityEngine;

namespace BalloonParty.Balloon.Formation
{
    /// <summary>
    ///     One actor and the slot it moves to, in a batch the balancer carries out at once: every actor leaves its
    ///     slot before any lands, so actors in the batch may take each other's slots.
    /// </summary>
    internal readonly struct SlotMove
    {
        public readonly IWriteableDynamicSlotActor Actor;
        public readonly Vector2Int Target;

        public SlotMove(IWriteableDynamicSlotActor actor, Vector2Int target)
        {
            Actor = actor;
            Target = target;
        }
    }
}
