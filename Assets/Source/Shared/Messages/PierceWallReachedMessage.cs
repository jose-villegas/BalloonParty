using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A shot that was piercing reached a wall: the straight run it plowed ends at
    ///     <see cref="Position" />. Published after the wall's discharge has resolved, so the shot's
    ///     <c>IsPiercing</c> already says whether the lance carries on past this wall. Also published for
    ///     the wall a piercing shot dies on.
    /// </summary>
    public readonly struct PierceWallReachedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 Position;

        public PierceWallReachedMessage(IProjectileModel projectile, Vector3 position)
        {
            Projectile = projectile;
            Position = position;
        }
    }
}
