using System.Collections.Generic;
using System.Linq;
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

        [Header("Balancing")]
        [SerializeField] private float _timeForBalloonsBalance;

        [Header("Nudge")]
        [SerializeField] private float _nudgeDistance = 0.3f;
        [SerializeField] private float _nudgeDuration = 0.15f;
        [SerializeField] private float _nudgeFalloff = 1.5f;

        public BalloonPrefabEntry[] Entries => _entries;
        public ParticleSystem DefaultPopVfxPrefab => _defaultPopVfxPrefab;
        public int GameStartedBalloonLines => _gameStartedBalloonLines;
        public int NewProjectileBalloonLines => _newProjectileBalloonLines;
        public float NewBalloonLinesTimeInterval => _newBalloonLinesTimeInterval;
        public Vector2 BalloonSpawnAnimationDurationRange => _balloonSpawnAnimationSpeedRange;
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
            // Build candidate list — skip entries that are at or over their max (0 = no limit)
            var candidates = _entries.Where(e =>
                e.MaxCount == 0 ||
                activeCounts.GetValueOrDefault(e.PoolKey) < e.MaxCount).ToArray();

            if (candidates.Length == 0)
            {
                return null;
            }

            var totalWeight = candidates.Sum(e => e.Weight);
            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;

            foreach (var entry in candidates)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    return entry;
                }
            }

            return candidates[0];
        }
    }
}
