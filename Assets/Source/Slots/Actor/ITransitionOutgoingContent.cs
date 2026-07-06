using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>Keeps outgoing visuals on screen during a level transition so the Ascent's board clear doesn't blink them out.</summary>
    internal interface ITransitionOutgoingContent
    {
        /// <summary>Parents current content under <paramref name="outgoingRoot" />, offset by <paramref name="exitDrop" /> so it slides out in lockstep with the incoming content.</summary>
        void HoldOutgoing(Transform outgoingRoot, float exitDrop);

        /// <summary>Called once the transition has settled — discard whatever was held.</summary>
        void ReleaseOutgoing();
    }
}
