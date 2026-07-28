using System;
using BalloonParty.Balloon.Type;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Balloons
{
    /// <summary>
    ///     One entry of a range's static <c>WeightedSet&lt;BalloonType&gt;</c>; membership is the type gate.
    /// </summary>
    [Serializable]
    public struct BalloonTypeWeight
    {
        [SerializeField] private BalloonType _type;
        [SerializeField] private float _weight;

        [Tooltip("Weighted chance of how many of this type the INITIAL board fill may add (X = count, Y = weight). " +
                 "The last keyframe's X value defines MaxCount for this type. Empty = uncapped.")]
        [SerializeField] private AnimationCurve _initialCountWeights;

        [Tooltip("Weighted chance of how many of this type each turn's spawn wave may add (X = count, Y = weight). " +
                 "Empty = defaults to the initial count curve.")]
        [SerializeField] private AnimationCurve _waveCountWeights;

        [Tooltip("Guaranteed minimum of this type placed during the initial board fill, before weighted " +
                 "picks run. 0 = no guarantee (purely weight-driven). Use for tutorialization: ensures " +
                 "the player sees at least N of a new type on the first board.")]
        [SerializeField] private int _guaranteedInitialCount;

        public BalloonType Type => _type;
        public float Weight => _weight;
        public AnimationCurve InitialCountWeights => _initialCountWeights;
        public AnimationCurve WaveCountWeights => _waveCountWeights;
        public int GuaranteedInitialCount => _guaranteedInitialCount;

        /// <summary>Inferred from the last keyframe X of <see cref="InitialCountWeights"/>. 0 = uncapped.</summary>
        public int MaxCount =>
            _initialCountWeights != null && _initialCountWeights.length > 0
                ? Mathf.RoundToInt(_initialCountWeights[_initialCountWeights.length - 1].time)
                : 0;

        public BalloonTypeWeight(BalloonType type, float weight, int guaranteedInitialCount = 0)
        {
            _type = type;
            _weight = weight;
            _initialCountWeights = null;
            _waveCountWeights = null;
            _guaranteedInitialCount = guaranteedInitialCount;
        }
    }
}
