using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Tail extrapolation configuration for <see cref="LevelScoringCurve"/>. Controls how the cumulative
    /// milestone grows beyond the last authored control point.</summary>
    [Serializable]
    internal struct TailGrowthConfig
    {
        [SerializeField] private TailGrowthMode _mode;

        [Tooltip("Geometric: multiplier per level (1.0 = flat, 1.1 = 10% growth). " +
                 "Linear: addend per level (0 = flat, 50 = +50 each level).")]
        [SerializeField] private float _rate;

        public TailGrowthMode Mode => _mode;
        public float Rate => _rate;

        internal TailGrowthConfig(TailGrowthMode mode, float rate)
        {
            _mode = mode;
            _rate = Mathf.Max(0f, rate);
        }
    }
}
