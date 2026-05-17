using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct BalloonScoredMessage
    {
        public readonly string ColorName;
        public readonly Vector3 WorldPosition;
        public readonly int Points;
        public readonly int CurrentProgress;

        public BalloonScoredMessage(string colorName, Vector3 worldPosition, int points, int currentProgress)
        {
            ColorName = colorName;
            WorldPosition = worldPosition;
            Points = points;
            CurrentProgress = currentProgress;
        }
    }
}
