using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Which levels are set on the ground and which in the sky; read through
    ///     <see cref="ScenarioSchedule" />.
    /// </summary>
    [Serializable]
    internal class ScenarioScheduleSettings
    {
        [Tooltip("Every level before this one is sky; this level is always ground, so whatever needs ground " +
                 "(bushes) first appears here.")]
        [SerializeField] [Min(1)] private int _firstGroundLevel = 16;

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

        [Tooltip("Mixed into the stretch rolls. Kept apart from the level seed salt, so retuning level " +
                 "content doesn't reshuffle which levels are ground.")]
        [SerializeField] private int _salt = 7919;

        public int FirstGroundLevel => Mathf.Max(1, _firstGroundLevel);
        public int FirstGroundStretch => Mathf.Max(1, _firstGroundStretch);
        public int MinGroundStretch => Mathf.Max(1, _minGroundStretch);
        public int MaxGroundStretch => Mathf.Max(MinGroundStretch, _maxGroundStretch);
        public int MinSkyStretch => Mathf.Max(1, _minSkyStretch);
        public int MaxSkyStretch => Mathf.Max(MinSkyStretch, _maxSkyStretch);
        public int Salt => _salt;
        internal bool IsStretchRangeValid => _minGroundStretch <= _maxGroundStretch && _minSkyStretch <= _maxSkyStretch;

        public ScenarioScheduleSettings()
        {
        }

        internal ScenarioScheduleSettings(int firstGroundLevel, int firstGroundStretch, int minGroundStretch,
            int maxGroundStretch, int minSkyStretch, int maxSkyStretch, int salt)
        {
            _firstGroundLevel = firstGroundLevel;
            _firstGroundStretch = firstGroundStretch;
            _minGroundStretch = minGroundStretch;
            _maxGroundStretch = maxGroundStretch;
            _minSkyStretch = minSkyStretch;
            _maxSkyStretch = maxSkyStretch;
            _salt = salt;
        }
    }
}
