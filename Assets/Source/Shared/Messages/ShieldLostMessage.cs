using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     A shield was spent absorbing a wall bounce. <see cref="Position" /> is the bounce point
    ///     (the projectile's clamped wall position) — where a shield trail flies to.
    /// </summary>
    public readonly struct ShieldLostMessage
    {
        public readonly Vector3 Position;

        public ShieldLostMessage(Vector3 position)
        {
            Position = position;
        }
    }
}
