using System;
using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>Setter surface <c>BalloonMotionTicker</c> drives during a nudge.</summary>
    internal interface IBalloonMotionView
    {
        /// <summary>True while a ticker-driven nudge is playing.</summary>
        bool IsNudging { get; }

        /// <summary>Sets the root position during a ticker-driven nudge.</summary>
        void ApplyNudgePosition(Vector3 position);

        /// <summary>Finishes a nudge; not called for cancelled nudges.</summary>
        void CompleteNudge(Action onComplete);

        /// <summary>Resets nudge state after an external cancellation (e.g. balance override).</summary>
        void OnNudgeCancelled();
    }
}
