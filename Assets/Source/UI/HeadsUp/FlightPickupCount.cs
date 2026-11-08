using System.Collections.Generic;
using BalloonParty.Projectile.Model;

namespace BalloonParty.UI.HeadsUp
{
    /// <summary>
    ///     How many shields each ball in the air has picked up since it was fired, so the bounce heads-up can
    ///     count up ("+1 bounce", "+2 bounce") over one flight and start again on the next.
    /// </summary>
    internal class FlightPickupCount
    {
        private readonly Dictionary<IProjectileModel, int> _counts = new();

        /// <summary>Counts one more pickup for <paramref name="projectile" /> and returns its total this flight.</summary>
        internal int Record(IProjectileModel projectile)
        {
            _counts.TryGetValue(projectile, out var count);
            _counts[projectile] = ++count;
            return count;
        }

        /// <summary>Drops <paramref name="projectile" />'s count once its flight ends.</summary>
        internal void Forget(IProjectileModel projectile)
        {
            _counts.Remove(projectile);
        }

        internal void Clear()
        {
            _counts.Clear();
        }
    }
}
