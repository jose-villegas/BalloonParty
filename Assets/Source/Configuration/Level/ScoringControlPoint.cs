using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>One authored milestone on the scoring curve. Defines the cumulative run-score
    /// the player should have earned by completing a specific level.</summary>
    [Serializable]
    internal struct ScoringControlPoint
    {
        [Min(1)]
        [SerializeField] private int _level;

        [Tooltip("Cumulative run-score the player should reach by completing this level (across all colours).")]
        [SerializeField] private float _cumulativeScore;

        [Tooltip("How the curve interpolates between this CP and the next. Only affects the segment after this point.")]
        [SerializeField] private SegmentMode _segmentMode;

        public int Level => _level;
        public float CumulativeScore => _cumulativeScore;
        public SegmentMode SegmentMode => _segmentMode;

        internal ScoringControlPoint(int level, float cumulativeScore, SegmentMode segmentMode = SegmentMode.Smooth)
        {
            _level = Mathf.Max(1, level);
            _cumulativeScore = cumulativeScore;
            _segmentMode = segmentMode;
        }
    }
}
