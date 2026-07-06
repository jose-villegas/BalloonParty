using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary><see cref="Position" /> is the bounce point (projectile's clamped wall position).</summary>
    public readonly struct ShieldLostMessage
    {
        public readonly Vector3 Position;

        public ShieldLostMessage(Vector3 position)
        {
            Position = position;
        }
    }
}
