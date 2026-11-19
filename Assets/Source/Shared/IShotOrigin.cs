using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     Where shots come from and how big the ball is, for the board-side code that plans or resolves
    ///     against a flight and cannot reach into the thrower's own scope. The thrower fills it in when it
    ///     starts (<c>ThrowerOriginProvider</c>).
    /// </summary>
    internal interface IShotOrigin
    {
        /// <summary>False until the thrower has started: the other two read as zero until then.</summary>
        bool IsAvailable { get; }

        /// <summary>
        ///     The point the cannon turns about when it is at rest. The muzzle sits straight ahead of it along the
        ///     aim, so the line of every shot, whatever its angle, passes through this one point.
        /// </summary>
        /// <remarks>
        ///     Captured once, when the thrower starts, and not read off the cannon as it stands: the cannon rises
        ///     half a unit as it enters and is punched back by each shot's recoil, and a layout planned in either
        ///     moment would be planned for a cannon that is not there when the level is played.
        /// </remarks>
        Vector2 RestingPivot { get; }

        /// <summary>The ball's own contact radius in world units.</summary>
        float ProjectileContactRadius { get; }
    }
}
