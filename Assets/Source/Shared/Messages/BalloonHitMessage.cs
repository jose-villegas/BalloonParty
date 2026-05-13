using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonHitMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 ProjectileDirection;

        public BalloonHitMessage(IBalloonModel balloon, Vector3 worldPosition, Vector3 projectileDirection)
        {
            Balloon = balloon;
            WorldPosition = worldPosition;
            ProjectileDirection = projectileDirection;
        }
    }
}
