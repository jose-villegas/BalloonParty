using BalloonParty.Projectile.View;

namespace BalloonParty.Thrower
{
    internal class ThrowerSettings
    {
        public readonly ProjectileView ProjectilePrefab;

        public ThrowerSettings(ProjectileView projectilePrefab)
        {
            ProjectilePrefab = projectilePrefab;
        }
    }
}
