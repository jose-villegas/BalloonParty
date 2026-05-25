using System;

namespace BalloonParty.Slots.Capabilities
{
    [Flags]
    public enum HitOutcome
    {
        None = 0,
        Deflect = 1 << 0,
        PassThrough = 1 << 1,
        Pop = 1 << 2,
        Absorb = 1 << 3,
        All = Deflect | PassThrough | Pop | Absorb
    }
}
