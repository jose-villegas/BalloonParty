using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Projectile" /> spent a shield; <see cref="Position" /> is the bounce point
    /// (the projectile's clamped wall position).</summary>
    public readonly struct ShieldLostMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 Position;

        public ShieldLostMessage(IProjectileModel projectile, Vector3 position)
        {
            Projectile = projectile;
            Position = position;
        }
    }
}
