using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Which levels are set in the sky, on the ground or over water; read through
    ///     <see cref="ScenarioSchedule" />.
    /// </summary>
    [Serializable]
    internal class ScenarioScheduleSettings
    {
        [Tooltip("Every level before this one is sky. From here the first ground stretch can start: its first " +
                 "level is rolled between this and Ground By Level, so whatever needs ground (bushes) can appear.")]
        [SerializeField] [Min(1)] private int _groundFromLevel = 6;

        [Tooltip("The first ground stretch has started by this level at the latest.")]
        [SerializeField] [Min(1)] private int _groundByLevel = 6;

        [Tooltip("Length of the first ground stretch, fixed rather than rolled: once the run reaches the ground " +
                 "it stays there at least this many levels.")]
        [SerializeField] [Min(1)] private int _firstGroundStretch = 4;

        [Tooltip("Fewest levels a later ground stretch lasts. Two flickers once the transition each way counts.")]
        [SerializeField] [Min(1)] private int _minGroundStretch = 3;

        [Tooltip("Most levels a later ground stretch lasts. Longer feels stuck on the harder ground.")]
        [SerializeField] [Min(1)] private int _maxGroundStretch = 5;

        [Tooltip("Fewest levels a sky stretch between two ground stretches lasts.")]
        [SerializeField] [Min(1)] private int _minSkyStretch = 2;

        [Tooltip("Most levels a sky stretch between two ground stretches lasts.")]
        [SerializeField] [Min(1)] private int _maxSkyStretch = 4;

        [Tooltip("Chance that a surface stretch after the first meadow is water rather than a meadow. Rolled " +
                 "after the stretch's length, so it changes which surface a stretch shows, never where it falls.")]
        [SerializeField] [Range(0f, 1f)] private float _waterShare = 0.5f;

        [Tooltip("The surface stretch after the first meadow is always water, so every run meets the water early " +
                 "instead of on a coin flip that can leave it for a long run. Later surfaces roll Water Share.")]
        [SerializeField] private bool _waterAfterFirstMeadow = true;

        [Tooltip("Mixed into the stretch rolls. Kept apart from the level seed salt, so retuning level " +
                 "content doesn't reshuffle which levels are ground.")]
        [SerializeField] private int _salt = 7919;

        public int GroundByLevel => Mathf.Max(1, _groundByLevel);
        public int GroundFromLevel => Mathf.Clamp(_groundFromLevel, 1, GroundByLevel);
        public int FirstGroundStretch => Mathf.Max(1, _firstGroundStretch);
        public int MinGroundStretch => Mathf.Max(1, _minGroundStretch);
        public int MaxGroundStretch => Mathf.Max(MinGroundStretch, _maxGroundStretch);
        public int MinSkyStretch => Mathf.Max(1, _minSkyStretch);
        public int MaxSkyStretch => Mathf.Max(MinSkyStretch, _maxSkyStretch);
        public float WaterShare => Mathf.Clamp01(_waterShare);
        public bool WaterAfterFirstMeadow => _waterAfterFirstMeadow;
        public int Salt => _salt;
        internal bool IsStretchRangeValid => _minGroundStretch <= _maxGroundStretch && _minSkyStretch <= _maxSkyStretch;

        public ScenarioScheduleSettings()
        {
        }

        // Without groundFromLevel the first ground level is groundByLevel, fixed.
        internal ScenarioScheduleSettings(int groundByLevel, int firstGroundStretch, int minGroundStretch,
            int maxGroundStretch, int minSkyStretch, int maxSkyStretch, int salt, int groundFromLevel = 0,
            float waterShare = 0f, bool waterAfterFirstMeadow = false)
        {
            _groundByLevel = groundByLevel;
            _groundFromLevel = groundFromLevel > 0 ? groundFromLevel : groundByLevel;
            _firstGroundStretch = firstGroundStretch;
            _minGroundStretch = minGroundStretch;
            _maxGroundStretch = maxGroundStretch;
            _minSkyStretch = minSkyStretch;
            _maxSkyStretch = maxSkyStretch;
            _salt = salt;
            _waterShare = waterShare;
            _waterAfterFirstMeadow = waterAfterFirstMeadow;
        }
    }
}
