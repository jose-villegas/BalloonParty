using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared
{
    public interface IGameConfiguration : IProjectileFlightConfig
    {
        int StartingHitPoints { get; }
        float ProjectileLoadDuration { get; }
        float ProjectileDisappearDuration { get; }
        Ease ProjectileDisappearEase { get; }
        float ProjectileDeadDriftFactor { get; }

        Vector2Int SlotsSize { get; }
        Vector2 SlotSeparation { get; }
        Vector2 SlotsOffset { get; }

        float PredictionTraceStep { get; }
        int PredictionTraceMaxBounces { get; }
        int PredictionTraceMaxSteps { get; }
        Color PredictionTraceColor { get; }

        float ScorePointTraceDuration { get; }
        float ScorePointsScatterDelay { get; }
        float ScorePointBurstDuration { get; }

        int ScoreTrailPrewarmPerColor { get; }
        int ProgressNoticePrewarmPerColor { get; }
    }
}
