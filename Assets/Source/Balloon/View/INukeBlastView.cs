using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     The nuke's blast as drawn and felt: a disc grown from the nuke to the radius the blast has reached, the light
    ///     it carries and the push it gives, and the implosion while its fuse runs.
    /// </summary>
    internal interface INukeBlastView : IGrowingDiscView
    {
        /// <summary>Pulls the disturbance field in at <paramref name="center" /> over the fuse's <paramref name="seconds" />.</summary>
        void Implode(Vector3 center, float seconds);
    }
}
