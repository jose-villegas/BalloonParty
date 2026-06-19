using UnityEngine;

namespace BalloonParty.Shared
{
    public interface IGameConfiguration
    {
        int ProjectileStartingShields { get; }
        int StartingHitPoints { get; }
        float ProjectileSpeed { get; }
        float ProjectileLoadDuration { get; }
        Vector4 LimitsClockwise { get; }
        float ShieldTrailDuration { get; }

        Vector2Int SlotsSize { get; }
        Vector2 SlotSeparation { get; }
        Vector2 SlotsOffset { get; }

        float PredictionTraceStep { get; }
        int PredictionTraceMaxBounces { get; }
        int PredictionTraceMaxSteps { get; }

        float ScorePointTraceDuration { get; }
        float ScorePointsScatterDelay { get; }
        float ScorePointBurstDuration { get; }

        int PointsRequiredForLevel(int level);
    }
}
