using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A chain of mushroom bounces armed <see cref="Projectile" /> as the Snipe lance
    ///     (@ref plan_meadow_mushrooms): published by <c>MushroomChainController</c> once the lance has been
    ///     granted, at the bounce that completed the chain. From there the ball bounces off no mushroom: it cuts
    ///     the caps in its way (<see cref="MushroomCutMessage" />).
    /// </summary>
    internal readonly struct MushroomLanceMessage
    {
        public readonly IProjectileModel Projectile;

        /// <summary>The ball's centre at the bounce that armed it.</summary>
        public readonly Vector3 Position;

        /// <summary>The chain's count at the bounce that armed it.</summary>
        public readonly int Count;

        public MushroomLanceMessage(IProjectileModel projectile, Vector3 position, int count)
        {
            Projectile = projectile;
            Position = position;
            Count = count;
        }
    }
}
