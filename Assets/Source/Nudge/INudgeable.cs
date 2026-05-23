using System;
using UnityEngine;

namespace BalloonParty.Nudge
{
    /// <summary>
    /// View-side contract for actors that can play a nudge animation.
    /// Decouples <c>NudgeService</c> from <c>BalloonView</c> specifically.
    /// </summary>
    internal interface INudgeable
    {
        void Nudge(
            Vector3 slotPosition,
            Vector3 direction,
            float nudgeDistance,
            float nudgeDuration,
            Action onComplete);
    }
}
