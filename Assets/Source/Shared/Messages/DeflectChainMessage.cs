using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Projectile" /> deflected off a balloon for the <see cref="Count" />th time in a row
    /// without touching a wall (from <see cref="DeflectChainRule.MinChain" /> on). <see cref="Position" /> is
    /// the deflecting balloon's. <see cref="PaidShield" /> says this deflect earned the ball a shield; its
    /// <see cref="ShieldGainedMessage" /> follows, marked <see cref="ShieldGainSource.Ricochet" />.</summary>
    public readonly struct DeflectChainMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly int Count;
        public readonly Vector3 Position;
        public readonly bool PaidShield;

        public DeflectChainMessage(IProjectileModel projectile, int count, Vector3 position, bool paidShield)
        {
            Projectile = projectile;
            Count = count;
            Position = position;
            PaidShield = paidShield;
        }
    }
}
