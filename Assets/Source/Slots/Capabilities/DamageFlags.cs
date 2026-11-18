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

        /// <summary>The shot (not rainbow-buffed) popped a rainbow it didn't paint: the pop climbs its streak
        /// whatever its colour, and the multiplier carries intact to the next colour the shot adopts. A shot
        /// with no colour yet climbs too, so a board of rainbows still builds a multiplier. (1 &lt;&lt; 3 was
        /// a colourless shot's banked rainbow pop, which paid ×1 until a colour came.)</summary>
        CarryStreak = 1 << 4,

        /// <summary>A laser beam struck the actor. The beam's damage also carries <see cref="Piercing" />,
        /// as a bomb's blast on its neighbours does; this tells the two apart, for the golden balloon, which
        /// a beam breaks and a blast must not.</summary>
        Beam = 1 << 5,

        /// <summary>The shot popped a balloon it painted rainbow itself (the disco ball's wave,
        /// <c>ShotPaintLedger</c>): the pop scores at the streak as it stands and leaves it unchanged, so a
        /// shot can't climb on paint it laid.</summary>
        OwnPaint = 1 << 6,

        /// <summary>A ripe bubble cluster's ripple struck the actor (<c>RipeBubbleBurst</c>). It lands after the pop
        /// that set it off, so it scores at the shot's streak as it stands and never changes it, not even to break it
        /// (<c>ScoreController.RecordStreakMultiplier</c>). It carries no <see cref="DirectHit" />, so it earns no
        /// pop-spawn either. Audio and telemetry can tell it apart by this flag.</summary>
        Ripple = 1 << 7
    }
}
