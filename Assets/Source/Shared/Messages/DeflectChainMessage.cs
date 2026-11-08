using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Projectile" /> deflected off a balloon for the <see cref="Count" />th time in a row
    /// without touching a wall (from <see cref="DeflectChainRule.MinChain" /> on). <see cref="Position" /> is
    /// the deflecting balloon's.</summary>
    public readonly struct DeflectChainMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly int Count;
        public readonly Vector3 Position;

        public DeflectChainMessage(IProjectileModel projectile, int count, Vector3 position)
        {
            Projectile = projectile;
            Count = count;
            Position = position;
        }
    }
}
