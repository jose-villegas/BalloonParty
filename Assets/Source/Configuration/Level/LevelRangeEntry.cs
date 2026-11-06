using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     An authored debug preset: a <see cref="RangedLevelParameters" /> mix keyed by a negative
    ///     <see cref="FromLevel" /> the start-level cheat selects. Positive levels are generated instead.
    /// </summary>
    [Serializable]
    public struct LevelRangeEntry
    {
        [Tooltip("The preset's ID: negative, unique, and what the start-level cheat selects (e.g. -100).")]
        [SerializeField] private int _fromLevel;

        [Tooltip("Negative marks a preset (use -1).")]
        [SerializeField] private int _toLevel;

        [SerializeField] private RangedLevelParameters _parameters;

        public int FromLevel => _fromLevel;
        public int ToLevel => _toLevel;
        public bool IsPreset => _fromLevel < 0 || _toLevel < 0;
        public RangedLevelParameters Parameters => _parameters;

        /// <summary>Use this outside the Inspector — the default struct ctor leaves <see cref="Parameters" /> null.</summary>
        public LevelRangeEntry(int fromLevel, int toLevel, RangedLevelParameters parameters)
        {
            _fromLevel = fromLevel;
            _toLevel = toLevel;
            _parameters = parameters;
        }
    }
}
