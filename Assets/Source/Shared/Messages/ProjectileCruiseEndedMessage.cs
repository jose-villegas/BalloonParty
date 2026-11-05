using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published when a cruising shot's run ends — it touched a balloon, or died on a wall.
    /// Pairs one-to-one, per shot, with <see cref="ProjectileCruiseStartedMessage"/> so feedback can tear down.</summary>
    internal readonly struct ProjectileCruiseEndedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 WorldPosition;

        public ProjectileCruiseEndedMessage(IProjectileModel projectile, Vector3 worldPosition)
        {
            Projectile = projectile;
            WorldPosition = worldPosition;
        }
    }
}
