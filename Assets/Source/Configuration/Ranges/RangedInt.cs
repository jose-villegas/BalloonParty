using System;
using UnityEngine;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Configuration.Ranges
{
    /// <summary>Pure — resolved once when a level begins, never re-rolled per turn.</summary>
    [Serializable]
    public struct RangedInt
    {
        [SerializeField] private int _min;
        [SerializeField] private int _max;
        [SerializeField] private RangeMode _mode;

        public RangedInt(int min, int max, RangeMode mode = RangeMode.Fixed)
        {
            _min = min;
            _max = max;
            _mode = mode;
        }

        public int Min => _min;
        public int Max => _max;
        public RangeMode Mode => _mode;

        /// <summary>Resolves a concrete value for a level's position within its range.</summary>
        /// <param name="positionInRange">0..1 position of the level within its range; ignored by Fixed/Random.</param>
        /// <param name="rng">Pass a seeded instance for determinism.</param>
        /// <returns>The resolved value for the given position and mode.</returns>
        public int Resolve(float positionInRange, System.Random rng)
        {
            switch (_mode)
            {
                case RangeMode.Linear:
                    return Mathf.RoundToInt(Mathf.Lerp(_min, _max, Mathf.Clamp01(positionInRange)));
                case RangeMode.Random:
                    return rng.Next(_min, _max + 1);
                default:
                    return _min;
            }
        }
    }
}
