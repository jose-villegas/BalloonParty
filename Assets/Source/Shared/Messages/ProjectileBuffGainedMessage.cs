using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A ball took on a kind of buff it didn't have: the rainbow (any colour keeps its streak) or a speed
    ///     buff. Another buff of a kind it already holds, such as a Snipe charge re-applying the rainbow,
    ///     sends nothing, and neither does one that expired the moment it was applied.
    /// </summary>
    public readonly struct ProjectileBuffGainedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly ProjectileBuffId BuffId;

        public ProjectileBuffGainedMessage(IProjectileModel projectile, ProjectileBuffId buffId)
        {
            Projectile = projectile;
            BuffId = buffId;
        }
    }
}
