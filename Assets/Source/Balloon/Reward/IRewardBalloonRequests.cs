using System;
using BalloonParty.Balloon.Type;

namespace BalloonParty.Balloon.Reward
{
    /// <summary>
    ///     The reward balloons a spawn line should bring. The spawner asks once per clock line, adds the one due to
    ///     that line past the level's own caps, and reports when it lands.
    /// </summary>
    internal interface IRewardBalloonRequests
    {
        /// <summary>
        ///     Whether this line brings a reward balloon, and which. Asked once per clock line: a heart rolls its chance
        ///     here, and the lines toward its cooldown count here. A golden balloon not yet placed stays due.
        /// </summary>
        bool TryTakeDue(out BalloonType type);

        /// <summary>
        ///     The reward balloon of <paramref name="type" /> is on the board. Returns what to call when it leaves it
        ///     (popped, cleared or pooled), which a run reset in between makes a no-op.
        /// </summary>
        Action Placed(BalloonType type);
    }
}
