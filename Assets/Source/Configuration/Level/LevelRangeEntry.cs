using System;
using UnityEngine;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Configuration.Level
{
    /// <summary>One contiguous span of levels sharing a <see cref="RangedLevelParameters" /> mix.</summary>
    [Serializable]
    public struct LevelRangeEntry
    {
        [SerializeField] private int _fromLevel;

        [Tooltip("<= 0 marks the open-ended tail.")]
        [SerializeField] private int _toLevel;

        [SerializeField] private RangedLevelParameters _parameters;

        /// <summary>Use this outside the Inspector — the default struct ctor leaves <see cref="Parameters" /> null.</summary>
        public LevelRangeEntry(int fromLevel, int toLevel, RangedLevelParameters parameters)
        {
            _fromLevel = fromLevel;
            _toLevel = toLevel;
            _parameters = parameters;
        }

        public int FromLevel => _fromLevel;
        public int ToLevel => _toLevel;
        public bool IsOpenEnded => _toLevel <= 0;
        public RangedLevelParameters Parameters => _parameters;

        public bool Contains(int level)
        {
            return level >= _fromLevel && (IsOpenEnded || level <= _toLevel);
        }

        /// <summary>0..1 position of <paramref name="level" /> within the range; 0 for the open-ended tail.</summary>
        public float PositionOf(int level)
        {
            if (IsOpenEnded)
            {
                return 0f;
            }

            var span = Mathf.Max(1, _toLevel - _fromLevel);
            return Mathf.Clamp01((float)(level - _fromLevel) / span);
        }
    }
}
