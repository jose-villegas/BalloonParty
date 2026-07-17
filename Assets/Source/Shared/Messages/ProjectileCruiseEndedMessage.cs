using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>Published when a cruising shot's run ends — it touched a balloon, or died on a wall.
    /// Pairs one-to-one with <see cref="ProjectileCruiseStartedMessage"/> so feedback can tear down.</summary>
    internal readonly struct ProjectileCruiseEndedMessage
    {
        public readonly Vector3 WorldPosition;

        public ProjectileCruiseEndedMessage(Vector3 worldPosition)
        {
            WorldPosition = worldPosition;
        }
    }
}
