using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     An int scalar authored once per <see cref="LevelRangeEntry" /> that resolves to a concrete
    ///     value per level: <see cref="RangeMode.Fixed" /> always returns <see cref="Min" />,
    ///     <see cref="RangeMode.Linear" /> ramps <see cref="Min" />→<see cref="Max" /> across the
    ///     range, <see cref="RangeMode.Random" /> rolls once per level within
    ///     [<see cref="Min" />, <see cref="Max" />]. Pure — resolved once when a level begins, never
    ///     re-rolled per turn.
    /// </summary>
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

        /// <param name="positionInRange">0..1 position of the level within its range; ignored by Fixed/Random.</param>
        /// <param name="rng">Source of randomness for Random mode — pass a seeded instance for determinism.</param>
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
