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

        [Tooltip("Fewest levels the run stays on one scenario before it may switch.")]
        [SerializeField] [Min(1)] private int _minStretch = 2;

        [Tooltip("Most levels the run stays on one scenario before it switches.")]
        [SerializeField] [Min(1)] private int _maxStretch = 4;

        [Tooltip("Mixed into the stretch rolls. Kept apart from the level seed salt, so retuning level " +
                 "content doesn't reshuffle which levels are ground.")]
        [SerializeField] private int _salt = 7919;

        public int FirstGroundLevel => Mathf.Max(1, _firstGroundLevel);
        public int MinStretch => Mathf.Max(1, _minStretch);
        public int MaxStretch => Mathf.Max(MinStretch, _maxStretch);
        public int Salt => _salt;
        internal bool IsStretchRangeValid => _minStretch <= _maxStretch;

        public ScenarioScheduleSettings()
        {
        }

        internal ScenarioScheduleSettings(int firstGroundLevel, int minStretch, int maxStretch, int salt)
        {
            _firstGroundLevel = firstGroundLevel;
            _minStretch = minStretch;
            _maxStretch = maxStretch;
            _salt = salt;
        }
    }
}
