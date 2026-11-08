using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Projectile" /> gained a shield from the actor at <see cref="SlotIndex" />, for the
    /// reason in <see cref="Source" />.</summary>
    public readonly struct ShieldGainedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector2Int SlotIndex;
        public readonly ShieldGainSource Source;

        public ShieldGainedMessage(IProjectileModel projectile, Vector2Int slotIndex,
            ShieldGainSource source = ShieldGainSource.Item)
        {
            Projectile = projectile;
            SlotIndex = slotIndex;
            Source = source;
        }
    }
}
