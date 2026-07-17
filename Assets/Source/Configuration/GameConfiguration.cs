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
