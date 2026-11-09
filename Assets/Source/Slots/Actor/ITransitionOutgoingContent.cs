using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>Keeps outgoing visuals on screen during a level transition so the Ascent's board clear doesn't blink them out.</summary>
    internal interface ITransitionOutgoingContent
    {
        /// <summary>Parents current content under <paramref name="outgoingRoot" /> (<see cref="ScenarioContentRoot.Outgoing" />), offset by <paramref name="exitDrop" /> so it slides out in lockstep with the incoming content. A mist reveal scales that holder about the camera, so held content must draw relative to its transform, not from world positions baked at hold time.</summary>
        void HoldOutgoing(Transform outgoingRoot, float exitDrop);

        /// <summary>Called once the transition has settled — discard whatever was held.</summary>
        void ReleaseOutgoing();
    }
}
