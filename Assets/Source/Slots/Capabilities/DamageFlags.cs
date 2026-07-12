using System;

namespace BalloonParty.Slots.Capabilities
{
    [Flags]
    public enum DamageFlags
    {
        Normal = 0,
        Piercing = 1 << 0,

        /// <summary>The hit should extend the score streak regardless of colour (a colour-agnostic,
        /// rainbow-buffed projectile) — see <c>ColorStreakTracker.RecordWildcard</c>.</summary>
        WildcardStreak = 1 << 1,

        /// <summary>The projectile itself struck the actor — absent from AOE/item damage, so per-pop
        /// reactions (e.g. pop-spawns) can exclude bombs/lasers/lightning.</summary>
        DirectHit = 1 << 2
    }
}
