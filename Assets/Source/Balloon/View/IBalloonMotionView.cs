using System;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     Setter surface <c>BalloonMotionTicker</c> drives: the controller owns the motion
    ///     state (nudge progress), the view owns the transform it writes to.
    /// </summary>
    internal interface IBalloonMotionView
    {
        /// <summary>Sets the root position during a ticker-driven nudge.</summary>
        void ApplyNudgePosition(Vector3 position);

        /// <summary>
        ///     Finishes a nudge: restores stability state and invokes the caller's callback.
        ///     Not called for cancelled nudges.
        /// </summary>
        void CompleteNudge(Action onComplete);
    }
}
