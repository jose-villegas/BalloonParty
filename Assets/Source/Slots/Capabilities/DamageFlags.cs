using System;

namespace BalloonParty.Slots.Capabilities
{
    [Flags]
    public enum DamageFlags
    {
        Normal   = 0,
        Piercing = 1 << 0,
    }
}


