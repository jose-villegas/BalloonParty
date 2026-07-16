using UnityEngine;

namespace BalloonParty.Balloon.View
{
    /// <summary>Position surface <c>BalloonMotionTicker</c> reads (base reconciliation) and writes (base + impulses).</summary>
    internal interface IBalloonMotionView
    {
        /// <summary>Transform.position passthrough.</summary>
        Vector3 Position { get; set; }
    }
}
