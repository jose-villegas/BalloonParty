using System;
using UnityEngine;

namespace BalloonParty.Nudge
{
    /// <summary>Decouples <c>NudgeService</c> from <c>BalloonView</c> specifically.</summary>
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
