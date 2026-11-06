using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     When balloon lines arrive: one line per interval of scaled game time, the interval shrinking
    ///     each level towards a floor. A formula rather than a per-range field, so it reaches every level
    ///     and survives the move to generated levels (PLAN-FastPaced Phase 4).
    /// </summary>
    [Serializable]
    internal class SpawnClockSettings : ISpawnClockSettings
    {
        [Tooltip("Seconds between balloon lines at level 1.")]
        [SerializeField] [Min(0.1f)] private float _firstLevelInterval = 3f;

        [Tooltip("Each level's interval is the previous level's times this: 0.96 = 4% faster per level. " +
                 "1 = the same pace at every level.")]
        [SerializeField] [Range(0.5f, 1f)] private float _intervalDecayPerLevel = 0.96f;

        [Tooltip("The interval never drops below this, however deep the level.")]
        [SerializeField] [Min(0.1f)] private float _minInterval = 1.25f;

        [Tooltip("Seconds of play after a level starts, or a run restarts, before its first line arrives.")]
        [SerializeField] [Min(0f)] private float _graceSeconds = 4f;

        [Tooltip("Lines grouped as one wave. Wave quotas (each balloon type's count-weights curve), roamer " +
                 "jumps and the item cadence count waves: they were tuned per turn, when a turn brought " +
                 "this many lines. 1 = every line is its own wave.")]
        [SerializeField] [Min(1)] private int _linesPerWave = 3;

        public float GraceSeconds => _graceSeconds;
        public int LinesPerWave => Mathf.Max(1, _linesPerWave);

        public SpawnClockSettings()
        {
        }

        internal SpawnClockSettings(float firstLevelInterval, float intervalDecayPerLevel, float minInterval,
            float graceSeconds, int linesPerWave = 1)
        {
            _firstLevelInterval = firstLevelInterval;
            _intervalDecayPerLevel = intervalDecayPerLevel;
            _minInterval = minInterval;
            _graceSeconds = graceSeconds;
            _linesPerWave = linesPerWave;
        }

        // Levels below 1 (the negative debug presets) run at level 1's pace.
        public float IntervalForLevel(int level)
        {
            var steps = Mathf.Max(0, level - 1);
            var interval = _firstLevelInterval * Mathf.Pow(_intervalDecayPerLevel, steps);
            return Mathf.Max(_minInterval, interval);
        }
    }
}
