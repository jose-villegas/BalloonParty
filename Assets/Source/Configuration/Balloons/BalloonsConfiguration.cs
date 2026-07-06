using System.Collections.Generic;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Balloons
{
    [CreateAssetMenu(menuName = "Configuration/Balloons Configuration", fileName = "BalloonsConfiguration")]
    public class BalloonsConfiguration : ScriptableObject, IBalloonsConfiguration
    {
        [SerializeField] private BalloonPrefabEntry[] _entries;

        [Header("Pop VFX")]
        [Tooltip("Default pop particle used for colored balloons. Tinted to the balloon's palette color at runtime.")]
        [SerializeField] private ParticleSystem _defaultPopVfxPrefab;

        [Header("Spawning")]
        [SerializeField] private int _gameStartedBalloonLines;
        [SerializeField] private int _newProjectileBalloonLines;
        [SerializeField] private float _newBalloonLinesTimeInterval;
        [SerializeField] private Vector2 _balloonSpawnAnimationSpeedRange;
        [Tooltip(
            "How many rows below the target slot the balloon enters from. Can exceed the grid bounds — the world position is still computed correctly.")]
        [SerializeField] private int _spawnEntryRowOffset = 4;

        [Header("Balancing")]
        [SerializeField] private float _timeForBalloonsBalance;

        [Tooltip("Seconds between board re-balances while a projectile is in flight — keeps the stack " +
                 "settling at intervals so a projectile looping wall-to-wall eventually finds a target. " +
                 "0 disables.")]
        [SerializeField] private float _flightRebalanceInterval = 1f;

        [Header("Nudge")]
        [SerializeField] private float _nudgeDistance = 0.3f;
        [SerializeField] private float _nudgeDuration = 0.15f;
        [SerializeField] private float _nudgeFalloff = 1.5f;

        public IReadOnlyList<BalloonPrefabEntry> Entries => _entries;
        public ParticleSystem DefaultPopVfxPrefab => _defaultPopVfxPrefab;
        public int GameStartedBalloonLines => _gameStartedBalloonLines;
        public int NewProjectileBalloonLines => _newProjectileBalloonLines;
        public float NewBalloonLinesTimeInterval => _newBalloonLinesTimeInterval;
        public Vector2 BalloonSpawnAnimationDurationRange => _balloonSpawnAnimationSpeedRange;
        public int SpawnEntryRowOffset => _spawnEntryRowOffset;
        public float TimeForBalloonsBalance => _timeForBalloonsBalance;
        public float FlightRebalanceInterval => _flightRebalanceInterval;
        public float NudgeDistance => _nudgeDistance;
        public float NudgeDuration => _nudgeDuration;
        public float NudgeFalloff => _nudgeFalloff;
    }
}
