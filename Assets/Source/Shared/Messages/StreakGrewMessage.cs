using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A ball's own pop just raised its streak to two or more: the moment a streak grew, where it grew.
    ///     Counted on the streak, never the score multiplier, so night doubling, item pops and a colour
    ///     bar that is already full never send it, and a multi-colour pop sends it once.
    /// </summary>
    public readonly struct StreakGrewMessage
    {
        /// <summary>The ball whose streak grew; null only for a direct hit no ball is tracked for.</summary>
        public readonly IProjectileModel Projectile;

        /// <summary>
        ///     The streak's colour. A rainbow-buffed ball climbs on any colour without changing it, so this
        ///     stays the last real colour it popped, and is null only if it has popped none yet.
        /// </summary>
        public readonly string Color;

        public readonly int Streak;
        public readonly Vector3 WorldPosition;

        public StreakGrewMessage(IProjectileModel projectile, string color, int streak, Vector3 worldPosition)
        {
            Projectile = projectile;
            Color = color;
            Streak = streak;
            WorldPosition = worldPosition;
        }
    }
}
