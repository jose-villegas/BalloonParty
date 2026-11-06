using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    /// <summary>The held ball has reached the muzzle with a slot free: the next release fires it.</summary>
    public readonly struct ThrowerReadyMessage
    {
        public IProjectileModel Projectile { get; }

        public ThrowerReadyMessage(IProjectileModel projectile)
        {
            Projectile = projectile;
        }
    }
}
