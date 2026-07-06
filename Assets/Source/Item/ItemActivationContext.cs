using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Passed as one value so adding hit info later doesn't churn every handler's signature.
    /// </summary>
    public readonly struct ItemActivationContext
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 ProjectileDirection;

        public ItemActivationContext(IBalloonModel balloon, Vector3 worldPosition, Vector3 projectileDirection)
        {
            Balloon = balloon;
            WorldPosition = worldPosition;
            ProjectileDirection = projectileDirection;
        }
    }
}
