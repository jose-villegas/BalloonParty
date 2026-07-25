using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>The projectile struck a wall. <see cref="Position" /> is the contact point. Today it is
    /// published alongside <see cref="ShieldLostMessage" /> (every bounce spends a shield), but it is a
    /// distinct event so wall hits and shield loss can diverge later without touching subscribers.</summary>
    public readonly struct WallHitMessage
    {
        public readonly Vector3 Position;

        public WallHitMessage(Vector3 position)
        {
            Position = position;
        }
    }
}
