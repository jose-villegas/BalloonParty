using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonDeflectedMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 BalloonWorldPosition;
        public readonly Vector3 ProjectileDirection;

        // The balloon collider's world radius — the deflect consumer adds its own contact radius to
        // reconstruct the exact swept-contact circle (see ProjectileMotionResolver.Deflect).
        public readonly float SurfaceRadius;

        public BalloonDeflectedMessage(
            IBalloonModel balloon,
            Vector3 balloonWorldPosition,
            Vector3 projectileDirection,
            float surfaceRadius)
        {
            Balloon = balloon;
            BalloonWorldPosition = balloonWorldPosition;
            ProjectileDirection = projectileDirection;
            SurfaceRadius = surfaceRadius;
        }
    }
}
