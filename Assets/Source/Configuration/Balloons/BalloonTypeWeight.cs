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

        public BalloonType Type => _type;
        public float Weight => _weight;
        public int MaxCountOverride => _maxCountOverride;

        public BalloonTypeWeight(BalloonType type, float weight, int maxCountOverride = 0)
        {
            _type = type;
            _weight = weight;
            _maxCountOverride = maxCountOverride;
        }
    }
}
