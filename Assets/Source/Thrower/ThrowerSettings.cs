#region

using BalloonParty.Projectile;

#endregion

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
