using BalloonParty.Projectile.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A shot that was piercing reached a wall: the straight run it plowed ends at
    ///     <see cref="Position" />. Published after the wall's discharge has resolved, so the shot's
    ///     <c>IsPiercing</c> already says whether the lance carries on past this wall. Also published for
    ///     the wall a piercing shot dies on. <see cref="Discharged" /> says whether the toughs it plowed
    ///     shattered at this wall — the run that spent the lance, as opposed to one it merely carried.
    /// </summary>
    public readonly struct PierceWallReachedMessage
    {
        public readonly IProjectileModel Projectile;
        public readonly Vector3 Position;
        public readonly bool Discharged;

        public PierceWallReachedMessage(IProjectileModel projectile, Vector3 position, bool discharged)
        {
            Projectile = projectile;
            Position = position;
            Discharged = discharged;
        }
    }
}
