using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published the instant a fired shot leaves the muzzle (its first free physics frame, so un-fired
    /// shots never emit it). Carries the shot, the muzzle world position and the fire heading so any system —
    /// the disturbance field, VFX, audio, camera shake — can react to the exact fire moment. This, not
    /// <see cref="ProjectileLoadedMessage" />, is when a shot's flight starts.</summary>
    internal readonly struct ProjectileFiredMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 WorldPosition;
        public readonly Vector3 Direction;

        public ProjectileFiredMessage(IProjectileModel projectile, Vector3 worldPosition, Vector3 direction)
        {
            Projectile = projectile;
            WorldPosition = worldPosition;
            Direction = direction;
        }
    }
}
