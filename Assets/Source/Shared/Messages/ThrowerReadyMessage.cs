using BalloonParty.Projectile.Model;

namespace BalloonParty.Shared.Messages
{
    /// <summary>The held ball has reached the muzzle with a slot free: the next release fires it.</summary>
    public readonly struct ThrowerReadyMessage
    {
        public IProjectileModel Projectile { get; }

        /// <summary>
        ///     Whether this ball waited with every slot under the ball cap taken before it got ready: the
        ///     cannon was held back by the cap, not only by its reload.
        /// </summary>
        public bool AfterCapWait { get; }

        public ThrowerReadyMessage(IProjectileModel projectile, bool afterCapWait)
        {
            Projectile = projectile;
            AfterCapWait = afterCapWait;
        }
    }
}
