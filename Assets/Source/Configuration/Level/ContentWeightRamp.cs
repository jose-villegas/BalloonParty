using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     A catalogue piece's weight by level: rolled within an early range at its unlock, moving to a late
    ///     range over <see cref="_rampLevels" />. Threats ramp up; aids usually fade as later ones arrive.
    /// </summary>
    [Serializable]
    internal struct ContentWeightRamp
    {
        [Tooltip("Weight on the unlock level, so the player meets the new piece. 0 = roll it like any level.")]
        [SerializeField] [Min(0f)] private float _introWeight;

        [Tooltip("Weight range just after unlock.")]
        [SerializeField] [Min(0f)] private float _minWeight;

        [SerializeField] [Min(0f)] private float _maxWeight;

        [Tooltip("Weight range once the ramp has run.")]
        [SerializeField] [Min(0f)] private float _lateMinWeight;

        [SerializeField] [Min(0f)] private float _lateMaxWeight;

        [Tooltip("Levels after unlock to move from the early range to the late one. 0 = late range at once.")]
        [SerializeField] [Min(0)] private int _rampLevels;

        /// <summary>The highest weight any level can roll; 0 means the piece never spawns.</summary>
        public float PeakWeight => Mathf.Max(Mathf.Max(_maxWeight, _lateMaxWeight), _introWeight);

        internal ContentWeightRamp(float minWeight, float maxWeight, float lateMinWeight, float lateMaxWeight,
            int rampLevels, float introWeight = 0f)
        {
            _minWeight = minWeight;
            _maxWeight = maxWeight;
            _lateMinWeight = lateMinWeight;
            _lateMaxWeight = lateMaxWeight;
            _rampLevels = rampLevels;
            _introWeight = introWeight;
        }

        /// <summary>A flat range: the same at every level.</summary>
        internal static ContentWeightRamp Flat(float minWeight, float maxWeight)
        {
            return new ContentWeightRamp(minWeight, maxWeight, minWeight, maxWeight, 0);
        }

        // Always draws once, so a piece's rolls take the same share of the stream at every level.
        internal float Roll(int level, int unlockLevel, System.Random rng)
        {
            var t = (float)rng.NextDouble();
            if (level == unlockLevel && _introWeight > 0f)
            {
                return _introWeight;
            }

            var progress = RampProgress(level, unlockLevel, _rampLevels);
            var min = Mathf.Lerp(_minWeight, _lateMinWeight, progress);
            var max = Mathf.Lerp(_maxWeight, _lateMaxWeight, progress);
            return max > min ? min + t * (max - min) : min;
        }

        /// <summary>
        ///     0 at <paramref name="unlockLevel" />, 1 once <paramref name="rampLevels" /> have passed;
        ///     1 throughout when there is no ramp.
        /// </summary>
        internal static float RampProgress(int level, int unlockLevel, int rampLevels)
        {
            return rampLevels > 0 ? Mathf.Clamp01((float)(level - unlockLevel) / rampLevels) : 1f;
        }

        internal bool IsValid()
        {
            return PeakWeight > 0f && _minWeight <= _maxWeight && _lateMinWeight <= _lateMaxWeight;
        }
    }
}
