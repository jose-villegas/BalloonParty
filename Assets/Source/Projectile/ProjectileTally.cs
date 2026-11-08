using System.Collections.Generic;
using BalloonParty.Projectile.Model;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     A running count per ball in the air: shields picked up this flight, deflects since the last wall.
    ///     The owner decides what counts and what breaks a count, and drops each ball once its flight ends.
    /// </summary>
    internal class ProjectileTally
    {
        private readonly Dictionary<IProjectileModel, int> _counts = new();

        /// <summary>Counts one more for <paramref name="projectile" /> and returns its running total.</summary>
        internal int Record(IProjectileModel projectile)
        {
            _counts.TryGetValue(projectile, out var count);
            _counts[projectile] = ++count;
            return count;
        }

        /// <summary>Starts <paramref name="projectile" />'s count over, or drops it once its flight ends.</summary>
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
