using BalloonParty.Shared;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Projectile Flight Config", fileName = "ProjectileFlightConfig")]
    internal class ProjectileFlightConfig : ScriptableObject, IProjectileFlightConfig
    {
        [Header("Loadout")]
        [SerializeField] private int _projectileStartingShields;

        [Tooltip("Wall-hit cue: with shields remaining at or above this, the tone stays on the root; each " +
                 "shield below steps it one degree down the scale (author WallHit as ScaleWalkDown). " +
                 "e.g. 5 = the descent begins once you drop below 5 shields.")]
        [SerializeField] [Min(0)] private int _shieldToneThreshold = 5;
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

        [Tooltip("Top-speed gain per earned TAP, as a fraction of base speed: speed = base x (1 + this x " +
                 "taps), cumulatively. Taps are minted by qualifying WALL HITS — an empty-corridor cruise " +
                 "bounce, or a clean Sweep past its threshold — never by shields (shields only happen to " +
                 "be spent at the same wall). Taps keep accruing for the whole flight, piercing included — " +
                 "Max Speed Multiplier is what bounds the result.")]
        [FormerlySerializedAs("_cruiseSpeedPerShield")]
        [SerializeField] [Min(0f)] private float _speedGainPerTap = 0.25f;

        [Tooltip("SAFETY RAIL, not a tuning knob: the hard ceiling on flight speed as a multiple of base " +
                 "speed, so taps and buffs together can never make one fixed step long enough to skip " +
                 "past geometry between steps. At 8 base speed and a 0.02 timestep, a step exceeds a " +
                 "balloon's chord (~0.875 wu, so the shot passes THROUGH it) above ~x5.5, and the play " +
                 "area itself above ~x28 — so x5.5 is the meaningful bound. Applies to the FINAL speed, " +
                 "buffs included. 0 = unlimited.")]
        [FormerlySerializedAs("_maxCruiseSpeedMultiplier")]
        [SerializeField] [Min(0f)] private float _maxSpeedMultiplier = 5.5f;

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

        [Tooltip("How many CONSECUTIVE segments the shot must spend breezing through 1-HP balloons (each " +
                 "leaving the corridor behind it clear) before sweeps start paying taps — a run length, the " +
                 "Sweep counterpart to Cruise Wall Bounce Threshold. Any wall reached without a clean pass " +
                 "breaks the run and restarts the count, exactly as a balloon contact breaks a cruise. " +
                 "0/1 = every clean pass pays.")]
        [SerializeField] [Min(0)] private int _sweepTapThreshold;

        [Header("Piercing")]
        [Tooltip("Cruise taps (bounces since entry) that arm PIERCING for the rest of the shot — it " +
                 "then pops everything it touches, unbreakables included. 0 disables.")]
        [SerializeField] [Min(0)] private int _cruisePiercingTapThreshold = 3;

        [Tooltip("Seconds an ARMED shot takes to accelerate into each new target speed. Armed taps use this " +
                 "ramp instead of the per-tap beat: it starts from the speed the shot already has, so the " +
                 "lance accelerates smoothly instead of dipping to a standstill every bounce. The arming tap " +
                 "is the first of these. 0 = instant.")]
        [SerializeField] [Min(0f)] private float _pierceArmRampDuration = 0.75f;

        [Tooltip("Shape of that acceleration, sampled over the ramp duration: 0 = the speed the shot armed " +
                 "at, 1 = its frozen top speed. Unlike the per-tap curve this blends between two real " +
                 "speeds, so a curve starting at 0 holds the old speed rather than stalling the shot.")]
        [SerializeField] private AnimationCurve _pierceArmRampCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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

        [Header("Hold Speed-Up")]
        [Tooltip("Maximum time-scale multiplier when holding during flight (e.g. 2 = double speed).")]
        [SerializeField] [Min(1f)] private float _holdSpeedUpMax = 2f;

        [Tooltip("Seconds to lerp from 1× to max while holding. 0 = instant.")]
        [SerializeField] [Min(0f)] private float _holdSpeedUpLerpDuration = 0.3f;

        [Tooltip("Seconds of uninterrupted flight before the hold-to-speed-up tooltip appears. 0 = disabled.")]
        [SerializeField] [Min(0f)] private float _holdSpeedUpTooltipDelay = 4f;

        public int ProjectileStartingShields => _projectileStartingShields;
        public int ShieldToneThreshold => _shieldToneThreshold;
        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLoadDuration => _projectileLoadDuration;
        public Vector4 LimitsClockwise => _limitsClockwise;
        public int CruiseWallBounceThreshold => _cruiseWallBounceThreshold;
        public float SpeedGainPerTap => _speedGainPerTap;
        public float MaxSpeedMultiplier => _maxSpeedMultiplier;
        public AnimationCurve CruiseTapCurve => _cruiseTapCurve;
        public float CruiseTapEaseDuration => _cruiseTapEaseDuration;
        public bool SweepEnabled => _sweepEnabled;
        public int SweepTapThreshold => _sweepTapThreshold;
        public int CruisePiercingTapThreshold => _cruisePiercingTapThreshold;
        public float PierceArmRampDuration => _pierceArmRampDuration;
        public AnimationCurve PierceArmRampCurve => _pierceArmRampCurve;
        public float PierceDischargeTimeScale => _pierceDischargeTimeScale;
        public float PierceDischargeTimeScaleDuration => _pierceDischargeTimeScaleDuration;
        public AnimationCurve LastShieldApproachCurve => _lastShieldApproachCurve;
        public float LastShieldApproachDuration => _lastShieldApproachDuration;
        public AnimationCurve LastShieldTimeScaleCurve => _lastShieldTimeScaleCurve;
        public float ShieldTrailDuration => _shieldTrailDuration;
        public float HoldSpeedUpMax => _holdSpeedUpMax;
        public float HoldSpeedUpLerpDuration => _holdSpeedUpLerpDuration;
        public float HoldSpeedUpTooltipDelay => _holdSpeedUpTooltipDelay;
    }
}
