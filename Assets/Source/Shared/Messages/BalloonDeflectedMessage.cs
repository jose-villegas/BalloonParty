using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonDeflectedMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 BalloonWorldPosition;
        public readonly Vector3 ProjectileDirection;

        public BalloonDeflectedMessage(
            IBalloonModel balloon,
            Vector3 balloonWorldPosition,
            Vector3 projectileDirection)
        {
            Balloon = balloon;
            BalloonWorldPosition = balloonWorldPosition;
            ProjectileDirection = projectileDirection;
        }
    }
}
