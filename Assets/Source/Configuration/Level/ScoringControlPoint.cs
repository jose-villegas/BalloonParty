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

        public int Level => _level;
        public float CumulativeScore => _cumulativeScore;

        internal ScoringControlPoint(int level, float cumulativeScore)
        {
            _level = Mathf.Max(1, level);
            _cumulativeScore = cumulativeScore;
        }
    }
}
