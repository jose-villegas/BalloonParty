using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Item
{
    /// <summary>
    ///     Everything an <see cref="IBalloonItem" /> needs about the hit that triggered it: the popped
    ///     balloon, where it popped, and the projectile's travel direction at hit time (zero if the
    ///     activation wasn't driven by a directional projectile hit). Passed as one value so adding hit
    ///     info later doesn't churn every handler's signature.
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
