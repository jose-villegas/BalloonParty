using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ScoreTrailArrivedMessage
    {
        public readonly string ColorName;
        public readonly int Score;
        public readonly int Level;
        public readonly Vector3 WorldPosition;

        public ScoreTrailArrivedMessage(string colorName, int score, int level, Vector3 worldPosition)
        {
            ColorName = colorName;
            Score = score;
            Level = level;
            WorldPosition = worldPosition;
        }
    }
}
