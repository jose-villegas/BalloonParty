using System;
using UnityEngine;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Configuration.Ranges
{
    /// <summary>Float counterpart of <see cref="RangedInt" /> — same resolution semantics.</summary>
    [Serializable]
    public struct RangedFloat
    {
        [SerializeField] private float _min;
        [SerializeField] private float _max;
        [SerializeField] private RangeMode _mode;

        public float Min => _min;
        public float Max => _max;
        public RangeMode Mode => _mode;

        public RangedFloat(float min, float max, RangeMode mode = RangeMode.Fixed)
        {
            _min = min;
            _max = max;
            _mode = mode;
        }

        /// <summary>Resolves a concrete value for a level's position within its range.</summary>
        /// <param name="positionInRange">0..1 position of the level within its range; ignored by Fixed/Random.</param>
        /// <param name="rng">Pass a seeded instance for determinism.</param>
        /// <returns>The resolved value for the given position and mode.</returns>
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
