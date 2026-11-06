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
        [SerializeField] [Min(0.1f)] private float _firstLevelInterval = 2f;

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

        [Tooltip("Share of the current wait a ball fired while another is still flying takes off the next " +
                 "line: 0.35 = it arrives 35% of an interval sooner. One ball at a time costs nothing, so " +
                 "rapid fire trades board space for firepower. 0 = firing never moves the clock.")]
        [SerializeField] [Range(0f, 1f)] private float _overlapShotPull = 0.35f;

        public float GraceSeconds => _graceSeconds;
        public float OverlapShotPull => _overlapShotPull;
        public int LinesPerWave => Mathf.Max(1, _linesPerWave);

        public SpawnClockSettings()
        {
        }

        internal SpawnClockSettings(float firstLevelInterval, float intervalDecayPerLevel, float minInterval,
            float graceSeconds, int linesPerWave = 1, float overlapShotPull = 0f)
        {
            _firstLevelInterval = firstLevelInterval;
            _intervalDecayPerLevel = intervalDecayPerLevel;
            _minInterval = minInterval;
            _graceSeconds = graceSeconds;
            _linesPerWave = linesPerWave;
            _overlapShotPull = overlapShotPull;
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
