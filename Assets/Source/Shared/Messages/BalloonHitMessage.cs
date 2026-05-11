using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonHitMessage
    {
        public readonly IBalloonModel Balloon;
        public readonly Vector3 WorldPosition;

        public BalloonHitMessage(IBalloonModel balloon, Vector3 worldPosition)
        {
            Balloon = balloon;
            WorldPosition = worldPosition;
        }
    }
}
