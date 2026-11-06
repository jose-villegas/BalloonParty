using BalloonParty.Projectile.Model;

namespace BalloonParty.Projectile
{
    /// <summary>
    ///     The "lone ball" rule for effects that reach beyond one ball: the doomed slow-mo, the board
    ///     freeze and the held spawn act only while the doomed ball is the only ball in flight. With
    ///     others flying, one ball's last breath must not slow or freeze the game under them.
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
    }
}
