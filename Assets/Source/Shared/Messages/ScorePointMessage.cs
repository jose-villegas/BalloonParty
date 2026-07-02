using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ScorePointMessage
    {
        public readonly string ColorName;
        public readonly Vector3 WorldPosition;
        public readonly int Score;
        public readonly int Level;
        public readonly int GroupSize;
        public readonly int GroupIndex;

        public ScorePointMessage(
            string colorName,
            Vector3 worldPosition,
            int score,
            int level,
            int groupSize,
            int groupIndex)
        {
            ColorName = colorName;
            WorldPosition = worldPosition;
            Score = score;
            Level = level;
            GroupSize = groupSize;
            GroupIndex = groupIndex;
        }
    }
}
