using System;

namespace BalloonParty.Nudge
{
    [Flags]
    public enum NudgeType
    {
        None = 0,
        Deflect = 1 << 0,
        Neighbor = 1 << 1,
        Shockwave = 1 << 2,
        Wave = 1 << 3,
        All = Deflect | Neighbor | Shockwave | Wave
    }
}
