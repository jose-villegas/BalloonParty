using BalloonParty.Balloon.Model;
using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonHitMessage
    {
        public readonly BalloonModel Balloon;
        public readonly Vector3 WorldPosition;

        public BalloonHitMessage(BalloonModel balloon, Vector3 worldPosition)
        {
            Balloon = balloon;
            WorldPosition = worldPosition;
        }
    }
}
