using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared
{
    public interface IGameConfiguration
    {
        int ProjectileStartingShields { get; }
        int StartingHitPoints { get; }
        float ProjectileSpeed { get; }
        float ProjectileLoadDuration { get; }
        float ProjectileDisappearDuration { get; }
        Ease ProjectileDisappearEase { get; }
        float ProjectileDeadDriftFactor { get; }
        Vector4 LimitsClockwise { get; }
        int CruiseWallBounceThreshold { get; }
        float CruiseSpeedPerShield { get; }
        float MaxCruiseSpeedMultiplier { get; }
        AnimationCurve CruiseTapCurve { get; }
        float CruiseTapEaseDuration { get; }
        int CruisePiercingTapThreshold { get; }

        // Seconds after the last tough/unbreakable a piercing shot plows before it discharges — shatters
        // the recorded toughs and slows to base. Re-armed by each plow, so a run of toughs holds it open.
        float PierceDischargeDelay { get; }
        AnimationCurve LastShieldApproachCurve { get; }
        float LastShieldApproachDuration { get; }
        AnimationCurve LastShieldTimeScaleCurve { get; }
        float ShieldTrailDuration { get; }

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
