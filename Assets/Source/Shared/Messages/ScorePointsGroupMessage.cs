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

        // The shot's travel direction at impact; BigScore rolls its shapes about it so the tumble reads as
        // momentum from the hit. Near-zero for non-projectile pops (items/lasers/board effects).
        public readonly Vector3 HitDirection;

        public int FirstScore => LastScore - Points + 1;

        public ScorePointsGroupMessage(
            string colorName,
            Vector3 worldPosition,
            int points,
            int lastScore,
            int multiplier,
            Vector3 hitDirection)
        {
            ColorName = colorName;
            WorldPosition = worldPosition;
            Points = points;
            LastScore = lastScore;
            Multiplier = multiplier;
            HitDirection = hitDirection;
        }
    }
}
