using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    internal readonly struct ScorePointsGroupMessage
    {
        public readonly string ColorName;
        public readonly Vector3 WorldPosition;
        public readonly int Points;
        public readonly int LastScore;
        public readonly int Multiplier;

        public int FirstScore => LastScore - Points + 1;

        public ScorePointsGroupMessage(
            string colorName,
            Vector3 worldPosition,
            int points,
            int lastScore,
            int multiplier)
        {
            ColorName = colorName;
            WorldPosition = worldPosition;
            Points = points;
            LastScore = lastScore;
            Multiplier = multiplier;
        }
    }
}
