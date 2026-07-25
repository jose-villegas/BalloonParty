using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>The projectile struck a wall. <see cref="Position" /> is the contact point and
    /// <see cref="ShieldsRemaining" /> is the shield count after this bounce. Published alongside
    /// <see cref="ShieldLostMessage" /> today (every bounce spends a shield), but a distinct event so
    /// wall hits and shield loss can diverge later without touching subscribers.</summary>
    public readonly struct WallHitMessage
    {
        public readonly Vector3 Position;
        public readonly int ShieldsRemaining;

        public WallHitMessage(Vector3 position, int shieldsRemaining)
        {
            Position = position;
            ShieldsRemaining = shieldsRemaining;
        }
    }
}
