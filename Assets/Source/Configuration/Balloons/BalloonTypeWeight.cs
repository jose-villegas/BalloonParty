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

        [Tooltip("0 = use the catalog BalloonPrefabEntry.MaxCount for this type.")]
        [SerializeField] private int _maxCountOverride;

        [Tooltip("Weighted chance of how many of this type the INITIAL board fill may add (X = count, Y = weight), like item counts. Empty = no per-fill cap.")]
        [SerializeField] private AnimationCurve _initialCountWeights;

        [Tooltip("Weighted chance of how many of this type each turn's spawn wave may add (X = count, Y = weight), like item counts. Empty = no per-wave cap.")]
        [SerializeField] private AnimationCurve _waveCountWeights;

        public BalloonType Type => _type;
        public float Weight => _weight;
        public int MaxCountOverride => _maxCountOverride;
        public AnimationCurve InitialCountWeights => _initialCountWeights;
        public AnimationCurve WaveCountWeights => _waveCountWeights;

        public BalloonTypeWeight(BalloonType type, float weight, int maxCountOverride = 0)
        {
            _type = type;
            _weight = weight;
            _maxCountOverride = maxCountOverride;
            _initialCountWeights = null;
            _waveCountWeights = null;
        }
    }
}
