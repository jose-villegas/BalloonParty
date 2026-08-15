using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    /// Read-only projectile flight configuration: physics, cruise, sweep, pierce, and doom tuning.
    /// A focused contract split out of the former umbrella game-configuration asset.
    /// </summary>
    public interface IProjectileFlightConfig
    {
        int ProjectileStartingShields { get; }
        int ShieldToneThreshold { get; }
        float ProjectileSpeed { get; }
        float ProjectileLoadDuration { get; }
        Vector4 LimitsClockwise { get; }
        int CruiseWallBounceThreshold { get; }
        float SpeedGainPerTap { get; }
        float MaxSpeedMultiplier { get; }
        AnimationCurve CruiseTapCurve { get; }
        float CruiseTapEaseDuration { get; }
        bool SweepEnabled { get; }
        int SweepTapThreshold { get; }
        int CruisePiercingTapThreshold { get; }
        float PierceArmRampDuration { get; }
        AnimationCurve PierceArmRampCurve { get; }
        float PierceDischargeTimeScale { get; }
        float PierceDischargeTimeScaleDuration { get; }
        AnimationCurve LastShieldApproachCurve { get; }
        float LastShieldApproachDuration { get; }
        AnimationCurve LastShieldTimeScaleCurve { get; }
        float ShieldTrailDuration { get; }

        /// <summary>Maximum time-scale multiplier when holding during flight (e.g. 2 = double speed).</summary>
        float HoldSpeedUpMax { get; }

        /// <summary>Seconds to lerp from 1× to <see cref="HoldSpeedUpMax"/> while holding.</summary>
        float HoldSpeedUpLerpDuration { get; }

        /// <summary>Seconds of flight before the "hold to speed up" tooltip appears (0 = disabled).</summary>
        float HoldSpeedUpTooltipDelay { get; }

        /// <summary>Minimum angular subdivision (degrees) the aim direction snaps to. 0 = continuous aim.</summary>
        float AimAngleStepDegrees { get; }

        /// <summary>
        /// Lower bound (degrees) of the reachable aim range, measured from +X the same way
        /// <c>ShotBoardGather.DirectionFromDegrees</c> does (0 = due right, 90 = straight up). Unlike
        /// <see cref="AimAngleStepDegrees"/> this is never "off" — the range always applies. Must stay
        /// below <see cref="AimAngleMaxDegrees"/>.
        /// </summary>
        float AimAngleMinDegrees { get; }

        /// <summary>Upper bound (degrees) of the reachable aim range — see <see cref="AimAngleMinDegrees"/>.</summary>
        float AimAngleMaxDegrees { get; }

        /// <summary>
        /// Seconds to look back before a release event when resolving the fired direction. A
        /// touchscreen lift-off registers as a position change (a finger rolling off the glass), so
        /// firing the aim from slightly before release keeps that displacement out of the shot.
        /// 0 = fire the live direction.
        /// </summary>
        float AimLatchSeconds { get; }
    }
}
