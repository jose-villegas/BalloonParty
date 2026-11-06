using BalloonParty.Balloon.Model;
using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ItemActivatedMessage
    {
        public readonly IBalloonModel Balloon;

        /// <summary>The ball that set the item off, or null when no ball did (a cheat, a chained item).</summary>
        public readonly IProjectileModel Projectile;

        public ItemActivatedMessage(IBalloonModel balloon, IProjectileModel projectile = null)
        {
            Balloon = balloon;
            Projectile = projectile;
        }
    }
}
