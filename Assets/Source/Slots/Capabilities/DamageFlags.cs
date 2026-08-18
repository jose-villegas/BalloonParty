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

        /// <summary>The projectile itself struck the actor, as opposed to an item/AOE effect (bomb, laser,
        /// lightning) popping it. Lets systems that trigger off a pop (e.g. spawning something from it)
        /// exclude those items, and gates whether a pop can grow the colour streak
        /// (<c>ScoreController.RecordStreakMultiplier</c>) — an item/AOE pop still scores at the live
        /// multiplier and still breaks the streak on a mismatch, it just can't climb it.</summary>
        DirectHit = 1 << 2,

        /// <summary>A colourless projectile popped a rainbow balloon — the streak contribution is
        /// deferred until the projectile adopts a colour on a subsequent hit.</summary>
        DeferredStreak = 1 << 3,

        /// <summary>A coloured projectile popped a rainbow — the streak multiplier should carry
        /// intact to the next colour the projectile adopts.</summary>
        CarryStreak = 1 << 4
    }
}
