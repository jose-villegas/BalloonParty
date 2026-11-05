using BalloonParty.Balloon.Model;
using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonDeflectedMessage
    {
        public readonly IBalloonModel Balloon;

        // The shot that bounced off — matched by the shot itself, since two shots can share a last-hit balloon.
        public readonly IProjectileModel Projectile;
        public readonly Vector3 BalloonWorldPosition;
        public readonly Vector3 ProjectileDirection;

        // The balloon collider's world radius — the deflect consumer adds its own contact radius to
        // reconstruct the exact swept-contact circle (see ProjectileMotionResolver.Deflect).
        public readonly float SurfaceRadius;

        public BalloonDeflectedMessage(
            IBalloonModel balloon,
            IProjectileModel projectile,
            Vector3 balloonWorldPosition,
            Vector3 projectileDirection,
            float surfaceRadius)
        {
            Balloon = balloon;
            Projectile = projectile;
            BalloonWorldPosition = balloonWorldPosition;
            ProjectileDirection = projectileDirection;
            SurfaceRadius = surfaceRadius;
        }
    }
}
