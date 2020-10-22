using System;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName = "Configuration/Game Configuration", fileName = "GameConfiguration")]
public class GameConfiguration : ScriptableObject, IGameConfiguration
{
    [Header("Display")] [SerializeField] private GameDisplayConfiguration _displayConfiguration;

    [Header("Thrower")] [SerializeField] private Vector2 _throwerSpawnPoint;

    [Header("Projectile")] [SerializeField]
    private Vector2 _projectileSpawnPoint;

    [SerializeField] private int _projectileStartingShields;

    [SerializeField] private float _projectileSpeed;
    [SerializeField] private Vector4 _limitsClockwise;

    [Header("Slots")] [SerializeField] private Vector2Int _slotsSize;
    [SerializeField] private Vector2 _slotSeparation;
    [SerializeField] private Vector2 _slotsOffset;

    [Header("Balloons")] [SerializeField] private Vector2 _balloonSpawnAnimationSpeedRange;
    [SerializeField] private int _gameStartedBalloonLines;
    [SerializeField] private BalloonColorConfiguration[] _balloonColors;
    [SerializeField] private float _timeForBalloonsBalance;
    [SerializeField] private int _newProjectileBalloonLines;
    [SerializeField] private float _newBalloonLinesTimeInterval;
    [SerializeField] private float _nudgeDistance;
    [SerializeField] private float _nudgeDuration;
    [SerializeField] private float _scorePointTraceDuration;

    [Header("Trace")] [SerializeField] private float _initialPredictionLength;
    [SerializeField] private int _predictionTraceMaxBounces;
    [SerializeField] private int _predictionTraceMaxSteps;

    [SerializeField] private PowerUpConfiguration _powerUpConfiguration;


    public Vector2 ThrowerSpawnPoint => _throwerSpawnPoint;

    public Vector2 ProjectileSpawnPoint => _projectileSpawnPoint;

    public float ProjectileSpeed => _projectileSpeed;

    public Vector4 LimitsClockwise => _limitsClockwise;

    public Vector2 SlotSeparation => _slotSeparation;

    public Vector2 SlotsOffset => _slotsOffset;

    public Vector2Int SlotsSize => _slotsSize;

    public Vector2 BalloonSpawnAnimationDurationRange => _balloonSpawnAnimationSpeedRange;

    public float NewBalloonLinesTimeInterval => _newBalloonLinesTimeInterval;

    public int GameStartedBalloonLines => _gameStartedBalloonLines;

    public BalloonColorConfiguration[] BalloonColors => _balloonColors;

    public float TimeForBalloonsBalance => _timeForBalloonsBalance;

    public int NewProjectileBalloonLines => _newProjectileBalloonLines;

    public float NudgeDistance => _nudgeDistance;

    public float NudgeDuration => _nudgeDuration;

    public GameDisplayConfiguration DisplayConfiguration => _displayConfiguration;

    public PowerUpConfiguration PowerUpConfiguration => _powerUpConfiguration;

    public int ProjectileStartingShields => _projectileStartingShields;

    public float PredictionTraceStep => _initialPredictionLength;

    public int PredictionTraceMaxBounces => _predictionTraceMaxBounces;

    public int PredictionTraceMaxSteps => _predictionTraceMaxSteps;

    public float ScorePointTraceDuration => _scorePointTraceDuration;

    public static int PointsRequiredForLevel(int level)
    {
        return (int) (Mathf.Exp(2) * Mathf.Log(Mathf.Pow(level, 2f * Mathf.PI)) + 25f);
    }

    int IGameConfiguration.PointsRequiredForLevel(int level)
    {
        return PointsRequiredForLevel(level);
    }

    public Color BalloonColor(string name)
    {
        return _balloonColors.First(x => x.Name == name).Color;
    }
}