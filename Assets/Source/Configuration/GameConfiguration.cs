using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Game Configuration", fileName = "GameConfiguration")]
    public class GameConfiguration : ScriptableObject, IGameConfiguration
    {
        [Header("Projectile")]
        [SerializeField] private int _projectileStartingShields;
        [SerializeField] private float _projectileSpeed;
        [SerializeField] private float _projectileLoadDuration;
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

        [Header("Score")]
        [SerializeField] private float _scorePointTraceDuration;
        [SerializeField] private float _scorePointsScatterDelay = 0.08f;

        public float ProjectileSpeed => _projectileSpeed;
        public float ProjectileLoadDuration => _projectileLoadDuration;
        public Vector4 LimitsClockwise => _limitsClockwise;
        public float ShieldTrailDuration => _shieldTrailDuration;
        public Vector2 SlotSeparation => _slotSeparation;
        public Vector2 SlotsOffset => _slotsOffset;
        public Vector2Int SlotsSize => _slotsSize;
        public int ProjectileStartingShields => _projectileStartingShields;
        public float PredictionTraceStep => _initialPredictionLength;
        public int PredictionTraceMaxBounces => _predictionTraceMaxBounces;
        public int PredictionTraceMaxSteps => _predictionTraceMaxSteps;
        public float ScorePointTraceDuration => _scorePointTraceDuration;
        public float ScorePointsScatterDelay => _scorePointsScatterDelay;

        public int PointsRequiredForLevel(int level)
        {
            return 3;
            return (int)((Mathf.Exp(2) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI))) + 25f);
        }
    }
}
