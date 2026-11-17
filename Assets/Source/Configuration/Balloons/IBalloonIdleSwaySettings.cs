using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     The plain balloon's idle sway, baked from its Animator controller so one ticker
    ///     (<c>BalloonSwayTicker</c>) plays it for every balloon in place of an Animator each. The controller stays
    ///     the place the sway is authored: <c>Tools ▸ BalloonParty ▸ Balloons ▸ Bake Idle Sway</c> reads it again.
    /// </summary>
    public interface IBalloonIdleSwaySettings
    {
        /// <summary>
        ///     The controller the sway was baked from. A balloon whose Animator plays this one is swayed by the
        ///     ticker and its Animator switched off; any other controller keeps its Animator. Null while unbaked.
        /// </summary>
        RuntimeAnimatorController Source { get; }

        /// <summary>The swayed child, as the clips name it: a path from the Animator's own transform.</summary>
        string PivotPath { get; }

        /// <summary>The sway of a balloon at rest in its slot.</summary>
        IBalloonSwayLoop Stable { get; }

        /// <summary>The wider sway of a balloon on the move.</summary>
        IBalloonSwayLoop Unstable { get; }
    }
}
