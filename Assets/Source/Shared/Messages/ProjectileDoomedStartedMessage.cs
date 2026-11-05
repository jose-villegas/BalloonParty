using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published when a shot enters its doomed final segment — 0 shields and a clear,
    /// shield-less path to the wall it will die on ('last breath'). Systems react by pausing
    /// (spawning) or claiming a slow-motion time scale for the moment.</summary>
    internal readonly struct ProjectileDoomedStartedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 WorldPosition;

        public ProjectileDoomedStartedMessage(IProjectileModel projectile, Vector3 worldPosition)
        {
            Projectile = projectile;
            WorldPosition = worldPosition;
        }
    }
}
