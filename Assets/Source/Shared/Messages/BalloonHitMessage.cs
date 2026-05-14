using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonHitMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 ProjectileDirection;

        /// <summary>
        ///     How many hit-points this hit removes from the balloon.
        ///     Defaults to 1 (normal projectile hit). Item handlers pass
        ///     their configured damage value from <see cref="Configuration.ItemSettings.Damage"/>.
        /// </summary>
        public readonly int Damage;

        public BalloonHitMessage(IBalloonModel balloon, Vector3 worldPosition, Vector3 projectileDirection, int damage = 1)
        {
            Balloon = balloon;
            WorldPosition = worldPosition;
            ProjectileDirection = projectileDirection;
            Damage = damage;
        }
    }
}
