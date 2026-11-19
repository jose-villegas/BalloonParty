using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    /// <summary>A shot's flight ended. <see cref="Projectile" /> is the shot that died; it has already
    /// left <c>IProjectileRegistry.InFlight</c> by the time this is published, so a subscriber asking
    /// whether anything is still flying gets the post-death answer.</summary>
    public readonly struct ProjectileDestroyedMessage
    {
        public readonly IProjectileModel Projectile;

        /// <summary>
        ///     True when the game took the shot off the board (the level-up cap, an emptied board, a clear of
        ///     the shots in flight) and not a wall: the flight was cut short, not lost. What counts how flights
        ///     end (a chain of mushrooms broken short of its target) leaves these out.
        /// </summary>
        public readonly bool Forced;

        public ProjectileDestroyedMessage(IProjectileModel projectile, bool forced = false)
        {
            Projectile = projectile;
            Forced = forced;
        }
    }
}
