using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     One contiguous span of levels sharing a <see cref="RangedLevelParameters" /> mix.
    ///     <see cref="ToLevel" /> &lt;= 0 marks the open-ended tail (applies forever). Ranges are
    ///     validated for contiguity/no-overlap/one tail in <see cref="LevelPacingConfiguration.OnValidate" />.
    /// </summary>
    [Serializable]
    public struct LevelRangeEntry
    {
        [SerializeField] private int _fromLevel;

        [Tooltip("<= 0 marks the open-ended tail.")]
        [SerializeField] private int _toLevel;

        [SerializeField] private RangedLevelParameters _parameters;

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
