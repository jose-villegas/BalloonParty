using BalloonParty.Projectile;

namespace BalloonParty.Thrower
{
    internal class ThrowerSettings
    {
        public readonly ProjectileLifetimeScope ProjectileScopePrefab;

        public ThrowerSettings(ProjectileLifetimeScope projectileScopePrefab)
        {
            ProjectileScopePrefab = projectileScopePrefab;
        }
    }
}
