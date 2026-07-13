using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace BalloonParty.Configuration.Level
{
    /// <summary>Pins the cumulative run-score a level should reach on completion (the curve's Y). The per-colour
    /// bar is derived from this — see <see cref="LevelPacingConfiguration.ThresholdForLevel" /> — as
    /// (this level's milestone − the previous level's) ÷ colours active that level.</summary>
    [Serializable]
    public struct LevelThresholdOverride
    {
        [SerializeField] private int _fromLevel;
        [SerializeField] private int _toLevel;

        [Tooltip("Y = cumulative run score to reach by completing the level (across all colours and prior levels). " +
                 "Sampled at t = 0..1 across the span, so t=0 is FromLevel and t=1 is ToLevel (a single-level " +
                 "override reads t=0). Keep it increasing per level, or the derived per-colour bar goes to zero.")]
        [FormerlySerializedAs("_requiredScore")]
        [SerializeField] private AnimationCurve _cumulativeScore;

        [Tooltip("Snap the derived per-colour bar down to the config's threshold-rounding multiple (like the " +
                 "formula path). Off = the exact value, rounded only to a whole number.")]
        [SerializeField] private bool _snapToRounding;

        public int FromLevel => _fromLevel;
        public int ToLevel => _toLevel;
        public bool SnapToRounding => _snapToRounding;

        public bool Contains(int level)
        {
            return level >= _fromLevel && level <= _toLevel;
        }

        public float CumulativeScore(int level)
        {
            var span = Mathf.Max(1, _toLevel - _fromLevel);
            var t = Mathf.Clamp01((float)(level - _fromLevel) / span);
            return _cumulativeScore.Evaluate(t);
        }
    }
}
