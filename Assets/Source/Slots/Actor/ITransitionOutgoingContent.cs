using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     A renderer of scenario content that can keep its current (outgoing) visuals on screen for
    ///     the duration of a level transition, so the old level's content stays visible while the new
    ///     level slides in over it — then drops them once the transition settles. The
    ///     level-transition Ascent clears the board before the new content arrives, which would
    ///     otherwise blink the outgoing content out.
    ///
    ///     HOW each implementer holds is its own business — the seam is deliberately general, not
    ///     cluster-specific: cluster views (one shared view per archetype) snapshot a frozen copy;
    ///     a future per-slot-rendered actor would instead retain its own outgoing views until release.
    /// </summary>
    internal interface ITransitionOutgoingContent
    {
        /// <summary>
        ///     Called before the board is cleared — start keeping the current content visible by
        ///     parenting it under <paramref name="outgoingRoot" /> (the descending scenario root),
        ///     offset one <paramref name="exitDrop" /> below the incoming content so as the root
        ///     descends this content slides down and out the bottom in lockstep with the new arriving.
        ///     <paramref name="exitDrop" /> matches the incoming content's lift height.
        /// </summary>
        void HoldOutgoing(Transform outgoingRoot, float exitDrop);

        /// <summary>Called once the transition has settled — discard whatever was held.</summary>
        void ReleaseOutgoing();
    }
}
