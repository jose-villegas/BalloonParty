using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>A wall hit minted a speed tap. <see cref="Position" /> is the wall contact and
    /// <see cref="TotalTaps" /> the shot's running tap count after it — the step a scale-walking cue
    /// climbs, so each tap sounds a degree higher. Deliberately source-agnostic: a cruise bounce and a
    /// clean sweep are two ways to earn the same rung, and only one can ever mint per wall hit
    /// (<c>ProjectileModelExtensions.TryGrantTap</c>), so this fires exactly once per rung earned.</summary>
    public readonly struct SpeedTapMintedMessage
    {
        public readonly Vector3 Position;
        public readonly int TotalTaps;

        public SpeedTapMintedMessage(Vector3 position, int totalTaps)
        {
            Position = position;
            TotalTaps = totalTaps;
        }
    }
}
