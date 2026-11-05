using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Projectile" /> struck a wall. <see cref="Position" /> is the contact point and
    /// <see cref="ShieldsRemaining" /> is the shield count after this bounce. Published alongside
    /// <see cref="ShieldLostMessage" /> today (every bounce spends a shield), but a distinct event so
    /// wall hits and shield loss can diverge later without touching subscribers.</summary>
    public readonly struct WallHitMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 Position;
        public readonly int ShieldsRemaining;

        public WallHitMessage(IProjectileModel projectile, Vector3 position, int shieldsRemaining)
        {
            Projectile = projectile;
            Position = position;
            ShieldsRemaining = shieldsRemaining;
        }
    }
}
