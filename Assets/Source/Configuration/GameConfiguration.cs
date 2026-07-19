using BalloonParty.Shared;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Game Configuration", fileName = "GameConfiguration")]
    public class GameConfiguration : ScriptableObject, IGameConfiguration
    {
        [Header("Run")]
        [SerializeField] private int _startingHitPoints = 5;

        [Header("Projectile")]
        [SerializeField] private int _projectileStartingShields;
        [SerializeField] private float _projectileSpeed;
        [SerializeField] private float _projectileLoadDuration;
        [SerializeField] private float _projectileDisappearDuration = 0.2f;
        [SerializeField] private Ease _projectileDisappearEase = Ease.InBack;
        [Tooltip("How far a dead shot drifts along its heading as it shrinks, as a multiple of speed×duration. 0 = stop in place.")]
        [SerializeField] private float _projectileDeadDriftFactor = 1f;
        [SerializeField] private Vector4 _limitsClockwise;
        [SerializeField] private float _shieldTrailDuration;

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

        [Tooltip("Cruise taps (bounces since entry) that arm PIERCING for the rest of the shot — it " +
                 "then pops everything it touches, unbreakables included. 0 disables.")]
        [SerializeField] [Min(0)] private int _cruisePiercingTapThreshold = 3;

        [Tooltip("Seconds after the last tough/unbreakable a piercing shot plows before it discharges " +
                 "(shatters the plowed toughs and slows to base). Re-armed by each plow, so a run of " +
                 "toughs holds it open; the discharge fires this long after the final one.")]
        [SerializeField] [Min(0f)] private float _pierceDischargeDelay = 0.12f;

        [Tooltip("Slow-mo dip at the pierce discharge: the time scale it drops to (1 = no dip).")]
        [SerializeField] [Range(0f, 1f)] private float _pierceDischargeTimeScale = 0.35f;

        [Tooltip("How long the pierce-discharge slow-mo dip holds, in UNSCALED seconds.")]
        [SerializeField] [Min(0f)] private float _pierceDischargeTimeScaleDuration = 0.15f;

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

        [Header("Slots")]
        [SerializeField] private Vector2Int _slotsSize;
        [SerializeField] private Vector2 _slotSeparation;
        [SerializeField] private Vector2 _slotsOffset;

        [Header("Trace")]
        [SerializeField] private float _initialPredictionLength;
        [SerializeField] private int _predictionTraceMaxBounces;
        [SerializeField] private int _predictionTraceMaxSteps;
        [Tooltip("Start/end color applied to the prediction trace LineRenderer.")]
        [SerializeField] private Color _predictionTraceColor = Color.white;

        [Header("Score")]
        [SerializeField] private float _scorePointTraceDuration;
        [SerializeField] private float _scorePointsScatterDelay = 0.08f;
        [SerializeField] private float _scorePointBurstDuration = 0.12f;

        [Tooltip("Per-color trail pool size prewarmed at level setup — a big pop or level-up ceremony " +
                 "can burst 20-40 live trails for one color.")]
        [SerializeField] private int _scoreTrailPrewarmPerColor = 64;

        [Tooltip("Per-color prewarm size for EACH of the point/streak notice pools — these rarely have " +
                 "more than a handful live at once, hence the lower default than the trail pool.")]
        [SerializeField] private int _progressNoticePrewarmPerColor = 16;

        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLoadDuration => _projectileLoadDuration;
        public float ProjectileDisappearDuration => _projectileDisappearDuration;
        public Ease ProjectileDisappearEase => _projectileDisappearEase;
        public float ProjectileDeadDriftFactor => _projectileDeadDriftFactor;
        public Vector4 LimitsClockwise => _limitsClockwise;
        public float ShieldTrailDuration => _shieldTrailDuration;
        public int CruiseWallBounceThreshold => _cruiseWallBounceThreshold;
        public float CruiseSpeedPerShield => _cruiseSpeedPerShield;
        public float MaxCruiseSpeedMultiplier => _maxCruiseSpeedMultiplier;
        public AnimationCurve CruiseTapCurve => _cruiseTapCurve;
        public float CruiseTapEaseDuration => _cruiseTapEaseDuration;
        public int CruisePiercingTapThreshold => _cruisePiercingTapThreshold;
        public float PierceDischargeDelay => _pierceDischargeDelay;
        public float PierceDischargeTimeScale => _pierceDischargeTimeScale;
        public float PierceDischargeTimeScaleDuration => _pierceDischargeTimeScaleDuration;
        public AnimationCurve LastShieldApproachCurve => _lastShieldApproachCurve;
        public float LastShieldApproachDuration => _lastShieldApproachDuration;
        public AnimationCurve LastShieldTimeScaleCurve => _lastShieldTimeScaleCurve;
        public Vector2 SlotSeparation => _slotSeparation;
        public Vector2 SlotsOffset => _slotsOffset;
        public Vector2Int SlotsSize => _slotsSize;
        public int ProjectileStartingShields => _projectileStartingShields;
        public int StartingHitPoints => _startingHitPoints;
        public float PredictionTraceStep => _initialPredictionLength;
        public int PredictionTraceMaxBounces => _predictionTraceMaxBounces;
        public int PredictionTraceMaxSteps => _predictionTraceMaxSteps;
        public Color PredictionTraceColor => _predictionTraceColor;
        public float ScorePointTraceDuration => _scorePointTraceDuration;
        public float ScorePointsScatterDelay => _scorePointsScatterDelay;
        public float ScorePointBurstDuration => _scorePointBurstDuration;
        public int ScoreTrailPrewarmPerColor => _scoreTrailPrewarmPerColor;
        public int ProgressNoticePrewarmPerColor => _progressNoticePrewarmPerColor;
    }
}
