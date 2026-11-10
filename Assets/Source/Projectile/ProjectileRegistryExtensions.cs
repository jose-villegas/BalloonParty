using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     The "lone ball" rule for effects that reach beyond one ball: the doomed slow-mo, the board
    ///     freeze and the held spawn act only while the doomed ball is the only ball in flight. With
    ///     others flying, one ball's last breath must not slow or freeze the game under them. Also where a
    ///     given ball is while it flies, for what is written at the ball.
    /// </summary>
    internal static class ProjectileRegistryExtensions
    {
        internal static bool FliesAlone(this IProjectileRegistry registry, IProjectileModel projectile)
        {
            return projectile != null && registry.InFlightCount == 1 && registry.InFlightAt(0) == projectile;
        }

        internal static bool DoomedShotFliesAlone(this IProjectileRegistry registry)
        {
            return registry.InFlightCount == 1 && registry.InFlightAt(0).IsLastShieldApproach.Value;
        }

        /// <summary>Where <paramref name="projectile" /> is, if it is in flight; false for a held or gone ball.</summary>
        internal static bool TryGetInFlightPosition(
            this IProjectileRegistry registry, IProjectileModel projectile, out Vector3 position)
        {
            for (var i = 0; i < registry.InFlightCount; i++)
            {
                if (registry.InFlightAt(i) == projectile)
                {
                    position = registry.InFlightPositionAt(i);
                    return true;
                }
            }

            position = default;
            return false;
        }
    }
}
