using BalloonParty.Projectile;

namespace BalloonParty.Thrower
{
    public class ThrowerSettings
    {
        public readonly ProjectileLifetimeScope ProjectileScopePrefab;

        public ThrowerSettings(ProjectileLifetimeScope projectileScopePrefab)
        {
            ProjectileScopePrefab = projectileScopePrefab;
        }
    }
}
