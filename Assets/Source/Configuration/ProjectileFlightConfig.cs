using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Projectile Flight Config", fileName = "ProjectileFlightConfig")]
    internal class ProjectileFlightConfig : ScriptableObject, IProjectileFlightConfig
    {
        [Header("Loadout")]
        [SerializeField] private int _projectileStartingShields;
        [SerializeField] private float _projectileSpeed;
        [SerializeField] private float _projectileLoadDuration;

        [Header("Play Area")]
        [Tooltip("Wall limits in clockwise order (top, right, bottom, left) — the billiard box the shot " +
                 "bounces inside.")]
        [SerializeField] private Vector4 _limitsClockwise;

        [Header("Cruise")]
        [Tooltip("Consecutive wall bounces with no balloon contact before the shot counts as CRUISING " +
                 "(the earned long-flight moment: feedback + shield-scaled acceleration). 0 disables.")]
        [SerializeField] [Min(0)] private int _cruiseWallBounceThreshold = 3;

        [Tooltip("Top-speed gain per shield banked at cruise entry: max multiplier = 1 + this x entry " +
                 "shields, so a 13-shield cruise tops out much faster than a 5-shield one.")]
        [SerializeField] [Min(0f)] private float _cruiseSpeedPerShield = 0.25f;

        [Tooltip("Hard ceiling on cruise speed as a multiple of the base speed. At extremely high " +
                 "shield counts the cumulative taps can push the shot fast enough to skip past wall " +
                 "geometry in a single fixed step — this cap prevents that. 0 = unlimited.")]
        [SerializeField] [Min(0f)] private float _maxCruiseSpeedMultiplier = 4f;

        [Tooltip("Per-bounce speed-change animation: replayed from t=0 on EVERY cruise bounce, scaling " +
                 "the new target speed by curve(elapsed/duration). Start the curve at 0 to freeze the " +
                 "shot for a beat before it picks up to the new speed; end at 1 for the full target.")]
        [SerializeField] private AnimationCurve _cruiseTapCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Tooltip("Seconds the per-bounce tap animation takes. 0 = instant speed changes (no freeze).")]
        [SerializeField] [Min(0f)] private float _cruiseTapEaseDuration = 0.25f;

        [Header("Sweep")]
        [Tooltip("Enables Sweep: a wall hit after popping at least one balloon this segment awards a tap " +
                 "if the backward corridor is now clear.")]
        [SerializeField] private bool _sweepEnabled = true;

        [Tooltip("How many successful sweeps (clear-corridor detections) must occur before taps actually " +
                 "start adding speed. Functions like CruiseWallBounceThreshold but for sweeps. 0 = " +
                 "immediate (first sweep awards speed).")]
        [SerializeField] [Min(0)] private int _sweepTapThreshold;

        [Header("Piercing")]
        [Tooltip("Cruise taps (bounces since entry) that arm PIERCING for the rest of the shot — it " +
                 "then pops everything it touches, unbreakables included. 0 disables.")]
        [SerializeField] [Min(0)] private int _cruisePiercingTapThreshold = 3;

        [Tooltip("Slow-mo dip at the pierce discharge: the time scale it drops to (1 = no dip).")]
        [SerializeField] [Range(0f, 1f)] private float _pierceDischargeTimeScale = 0.35f;

        [Tooltip("How long the pierce-discharge slow-mo dip holds, in UNSCALED seconds.")]
        [SerializeField] [Min(0f)] private float _pierceDischargeTimeScaleDuration = 0.15f;

        [Header("Shield Trail")]
        [Tooltip("Seconds the shield-loss trail lingers behind the shot.")]
        [SerializeField] private float _shieldTrailDuration;

        [Header("Last Breath")]

        [Tooltip("Position easing across a doomed 0-shield segment, sampled on NORMALIZED TIME over " +
                 "LastShieldApproachDuration (x = time 0..1, y = distance fraction from last bounce to " +
                 "the death wall, 0->1). The 'last breath' — same wall-clock length whatever the " +
                 "segment's length. Ease-out = drift slower into the wall.")]
        [SerializeField] private AnimationCurve _lastShieldApproachCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Seconds the last-breath approach takes to cross its doomed segment. The whole moment " +
                 "is normalized to this GAME time (so it stretches under the slow-mo below). 0 disables.")]
        [SerializeField] [Min(0f)] private float _lastShieldApproachDuration = 3f;

        [Tooltip("Time-scale envelope across the doomed last-breath approach, sampled on NORMALIZED " +
                 "TIME (x = 0..1 from doom start to death wall). Y = time scale (0 = frozen, 1 = full " +
                 "speed). Replaces the flat LastShieldTimeScale — author a curve that eases the " +
                 "slow-mo in and/or out for dramatic pacing.")]
        [SerializeField] private AnimationCurve _lastShieldTimeScaleCurve = AnimationCurve.Constant(0f, 1f, 0.3f);

        public int ProjectileStartingShields => _projectileStartingShields;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLoadDuration => _projectileLoadDuration;
        public Vector4 LimitsClockwise => _limitsClockwise;
        public int CruiseWallBounceThreshold => _cruiseWallBounceThreshold;
        public float CruiseSpeedPerShield => _cruiseSpeedPerShield;
        public float MaxCruiseSpeedMultiplier => _maxCruiseSpeedMultiplier;
        public AnimationCurve CruiseTapCurve => _cruiseTapCurve;
        public float CruiseTapEaseDuration => _cruiseTapEaseDuration;
        public bool SweepEnabled => _sweepEnabled;
        public int SweepTapThreshold => _sweepTapThreshold;
        public int CruisePiercingTapThreshold => _cruisePiercingTapThreshold;
        public float PierceDischargeTimeScale => _pierceDischargeTimeScale;
        public float PierceDischargeTimeScaleDuration => _pierceDischargeTimeScaleDuration;
        public AnimationCurve LastShieldApproachCurve => _lastShieldApproachCurve;
        public float LastShieldApproachDuration => _lastShieldApproachDuration;
        public AnimationCurve LastShieldTimeScaleCurve => _lastShieldTimeScaleCurve;
        public float ShieldTrailDuration => _shieldTrailDuration;
    }
}
