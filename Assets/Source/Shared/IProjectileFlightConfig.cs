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
        /// <summary>
        ///     Seconds the held ball takes to reach the muzzle while no ball is flying: the near-instant
        ///     reload. Also the floor of <see cref="ReloadCooldownSeconds" />.
        /// </summary>
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

        /// <summary>
        ///     A piercing shot in flight (a Snipe lance, a cruise pierce) reports two blasts along each stretch it
        ///     flies, which on water part it under the shot: a wake. This is the first's world radius: wide and too
        ///     weak to bare the floor (<see cref="PierceWakeWaveStrength" />), it throws the wave out to both sides.
        /// </summary>
        float PierceWakeWaveRadius { get; }

        /// <summary>How hard the wave's blast throws, a bomb being 1; 0 reports none.</summary>
        float PierceWakeWaveStrength { get; }

        /// <summary>
        ///     The second blast's world radius. Narrow and strong (<see cref="PierceWakeHeartStrength" />), it is
        ///     the thin sliver of floor the water bares under the shot's path.
        /// </summary>
        float PierceWakeHeartRadius { get; }

        /// <summary>How hard the heart's blast throws, a bomb being 1; 0 reports none.</summary>
        float PierceWakeHeartStrength { get; }

        /// <summary>
        ///     The one blast a piercing shot reports at the wall its charge ends at (where it shatters what it plowed,
        ///     or dies): the world radius it clears. On water, a small crater at the end of the wake.
        /// </summary>
        float PierceWallImpactRadius { get; }

        /// <summary>How hard the wall's blast throws, a bomb being 1; 0 reports none.</summary>
        float PierceWallImpactStrength { get; }

        /// <summary>Seconds the wall's blast goes on throwing, hardest at the start.</summary>
        float PierceWallImpactSeconds { get; }
        AnimationCurve LastShieldApproachCurve { get; }
        float LastShieldApproachDuration { get; }
        AnimationCurve LastShieldTimeScaleCurve { get; }

        /// <summary>Most balls in flight at once; at the cap a release fires nothing.</summary>
        int MaxBallsInFlight { get; }

        /// <summary>
        ///     Seconds the held ball takes to reach the muzzle while another ball is flying (at least
        ///     <see cref="ProjectileLoadDuration" />), before reload cards.
        /// </summary>
        float ReloadCooldownSeconds { get; }

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
