using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    /// <summary>A shot's flight ended. <see cref="Projectile" /> is the shot that died; it has already
    /// left <c>IProjectileRegistry.InFlight</c> by the time this is published, so a subscriber asking
    /// whether anything is still flying gets the post-death answer.</summary>
    public readonly struct ProjectileDestroyedMessage
    {
        public readonly IProjectileModel Projectile;

        public ProjectileDestroyedMessage(IProjectileModel projectile)
        {
            Projectile = projectile;
        }
    }
}
