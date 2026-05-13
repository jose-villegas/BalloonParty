using System;

namespace BalloonParty.Shared
{
    [Flags]
    public enum NudgeType
    {
        None     = 0,
        Deflect  = 1 << 0,
        Neighbor = 1 << 1,
        All      = Deflect | Neighbor
    }
}

