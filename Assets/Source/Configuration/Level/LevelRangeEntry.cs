using System;
using UnityEngine;
using BalloonParty.Configuration.Level;

namespace BalloonParty.Configuration.Level
{
    /// <summary>One contiguous span of levels sharing a <see cref="RangedLevelParameters" /> mix.</summary>
    [Serializable]
    public struct LevelRangeEntry
    {
        [Tooltip("Negative marks a fallback range; the value serves as a unique ID loadable via cheats (e.g. -999).")]
        [SerializeField] private int _fromLevel;

        [Tooltip("Negative marks a fallback range (use -1 as the 'to' bound).")]
        [SerializeField] private int _toLevel;

        [SerializeField] private RangedLevelParameters _parameters;

        public int FromLevel => _fromLevel;
        public int ToLevel => _toLevel;
        public bool IsFallback => _fromLevel < 0 || _toLevel < 0;
        public bool IsOpenEnded => _toLevel <= 0;
        public RangedLevelParameters Parameters => _parameters;

        /// <summary>Use this outside the Inspector — the default struct ctor leaves <see cref="Parameters" /> null.</summary>
        public LevelRangeEntry(int fromLevel, int toLevel, RangedLevelParameters parameters)
        {
            _fromLevel = fromLevel;
            _toLevel = toLevel;
            _parameters = parameters;
        }

        public bool Contains(int level)
        {
            if (IsFallback)
            {
                return false;
            }

            return level >= _fromLevel && (IsOpenEnded || level <= _toLevel);
        }

        /// <summary>0..1 position of <paramref name="level" /> within the range; 0 for fallbacks and open-ended tails.</summary>
        public float PositionOf(int level)
        {
            if (IsFallback)
            {
                return 0f;
            }

            if (IsOpenEnded)
            {
                return 0f;
            }

            var span = Mathf.Max(1, _toLevel - _fromLevel);
            return Mathf.Clamp01((float)(level - _fromLevel) / span);
        }
    }
}
