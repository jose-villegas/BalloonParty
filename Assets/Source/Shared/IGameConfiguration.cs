using BalloonParty.Configuration;
using UnityEngine;

namespace BalloonParty.Shared
{
    public interface IGameConfiguration
    {
        float ProjectileSpeed { get; }
        Vector4 LimitsClockwise { get; }
        Vector2Int SlotsSize { get; }
        Vector2 SlotSeparation { get; }
        Vector2 SlotsOffset { get; }
        Vector2 BalloonSpawnAnimationDurationRange { get; }
        int GameStartedBalloonLines { get; }
        float TimeForBalloonsBalance { get; }
        int NewProjectileBalloonLines { get; }
        float NewBalloonLinesTimeInterval { get; }
        float NudgeDistance { get; }
        float NudgeDuration { get; }
        float ScorePointTraceDuration { get; }
        float ShieldTrailDuration { get; }
        int ProjectileStartingShields { get; }
        float PredictionTraceStep { get; }
        int PredictionTraceMaxBounces { get; }
        int PredictionTraceMaxSteps { get; }
        BalloonColorConfiguration[] BalloonColors { get; }

        int PointsRequiredForLevel(int level);
        Color BalloonColor(string colorName);
    }
}
