using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>Float counterpart of <see cref="RangedInt" /> — same resolution semantics.</summary>
    [Serializable]
    public struct RangedFloat
    {
        [SerializeField] private float _min;
        [SerializeField] private float _max;
        [SerializeField] private RangeMode _mode;

        public RangedFloat(float min, float max, RangeMode mode = RangeMode.Fixed)
        {
            _min = min;
            _max = max;
            _mode = mode;
        }

        public float Min => _min;
        public float Max => _max;
        public RangeMode Mode => _mode;

        /// <param name="positionInRange">0..1 position of the level within its range; ignored by Fixed/Random.</param>
        /// <param name="rng">Source of randomness for Random mode — pass a seeded instance for determinism.</param>
        public float Resolve(float positionInRange, System.Random rng)
        {
            switch (_mode)
            {
                case RangeMode.Linear:
                    return Mathf.Lerp(_min, _max, Mathf.Clamp01(positionInRange));
                case RangeMode.Random:
                    return _min + (float)(rng.NextDouble() * (_max - _min));
                default:
                    return _min;
            }
        }
    }
}
