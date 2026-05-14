using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonScoredMessage
    {
        public readonly string ColorName;
        public readonly Vector3 WorldPosition;
        public readonly int TotalScore;
        public readonly int Points;

        public BalloonScoredMessage(string colorName, Vector3 worldPosition, int totalScore, int points)
        {
            ColorName     = colorName;
            WorldPosition = worldPosition;
            TotalScore    = totalScore;
            Points        = points;
        }
    }
}
