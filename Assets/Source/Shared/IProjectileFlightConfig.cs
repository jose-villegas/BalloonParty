using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    /// Read-only projectile flight configuration: physics, cruise, sweep, pierce, and doom tuning.
    /// Extracted from <see cref="IGameConfiguration"/> to give consumers a focused contract.
    /// </summary>
    public interface IProjectileFlightConfig
    {
        int ProjectileStartingShields { get; }
        float ProjectileSpeed { get; }
        Vector4 LimitsClockwise { get; }
        int CruiseWallBounceThreshold { get; }
        float CruiseSpeedPerShield { get; }
        float MaxCruiseSpeedMultiplier { get; }
        AnimationCurve CruiseTapCurve { get; }
        float CruiseTapEaseDuration { get; }
        bool SweepEnabled { get; }
        int SweepTapThreshold { get; }
        int CruisePiercingTapThreshold { get; }
        float PierceDischargeTimeScale { get; }
        float PierceDischargeTimeScaleDuration { get; }
        AnimationCurve LastShieldApproachCurve { get; }
        float LastShieldApproachDuration { get; }
        AnimationCurve LastShieldTimeScaleCurve { get; }
        float ShieldTrailDuration { get; }
    }
}
