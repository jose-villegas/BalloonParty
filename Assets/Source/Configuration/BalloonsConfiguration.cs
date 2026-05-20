using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Balloons Configuration", fileName = "BalloonsConfiguration")]
    public class BalloonsConfiguration : ScriptableObject
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
        [Tooltip("How many rows below the target slot the balloon enters from. Can exceed the grid bounds — the world position is still computed correctly.")]
        [SerializeField] private int _spawnEntryRowOffset = 4;

        [Header("Balancing")]
        [SerializeField] private float _timeForBalloonsBalance;

        [Header("Nudge")]
        [SerializeField] private float _nudgeDistance = 0.3f;
        [SerializeField] private float _nudgeDuration = 0.15f;
        [SerializeField] private float _nudgeFalloff = 1.5f;

        private readonly List<BalloonPrefabEntry> _candidateBuffer = new();

        public BalloonPrefabEntry[] Entries => _entries;
        public ParticleSystem DefaultPopVfxPrefab => _defaultPopVfxPrefab;
        public int GameStartedBalloonLines => _gameStartedBalloonLines;
        public int NewProjectileBalloonLines => _newProjectileBalloonLines;
        public float NewBalloonLinesTimeInterval => _newBalloonLinesTimeInterval;
        public Vector2 BalloonSpawnAnimationDurationRange => _balloonSpawnAnimationSpeedRange;
        public int SpawnEntryRowOffset => _spawnEntryRowOffset;
        public float TimeForBalloonsBalance => _timeForBalloonsBalance;
        public float NudgeDistance => _nudgeDistance;
        public float NudgeDuration => _nudgeDuration;
        public float NudgeFalloff => _nudgeFalloff;


        /// <summary>
        /// Picks a random entry using weighted random selection, excluding entries that have
        /// reached their <see cref="BalloonPrefabEntry.MaxCount"/> limit.
        /// Returns null if all entries are at their limit.
        /// </summary>
        public BalloonPrefabEntry PickRandom(IReadOnlyDictionary<string, int> activeCounts)
        {
            _candidateBuffer.Clear();
            var totalWeight = 0f;

            foreach (var e in _entries)
            {
                if (e.MaxCount != 0 && activeCounts.GetValueOrDefault(e.PoolKey) >= e.MaxCount)
                {
                    continue;
                }

                _candidateBuffer.Add(e);
                totalWeight += e.Weight;
            }

            if (_candidateBuffer.Count == 0)
            {
                return null;
            }

            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;

            foreach (var entry in _candidateBuffer)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    return entry;
                }
            }

            return _candidateBuffer[0];
        }
    }
}
