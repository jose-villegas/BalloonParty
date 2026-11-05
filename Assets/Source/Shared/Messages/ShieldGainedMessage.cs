using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Projectile" /> gained a shield from the actor at <see cref="SlotIndex" />.</summary>
    public readonly struct ShieldGainedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector2Int SlotIndex;

        public ShieldGainedMessage(IProjectileModel projectile, Vector2Int slotIndex)
        {
            Projectile = projectile;
            SlotIndex = slotIndex;
        }
    }
}
