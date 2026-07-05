using System;
using BalloonParty.Balloon.Type;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     One entry of a range's static <c>WeightedSet&lt;BalloonType&gt;</c>. Membership <em>is</em>
    ///     the type gate — a type absent from a range's set cannot spawn in that range; a type present
    ///     with weight 0 is equivalent to absent. Weights are static per range (no <see cref="RangeMode" />)
    ///     — the per-spawn weighted draw already supplies the randomness.
    /// </summary>
    [Serializable]
    public struct BalloonTypeWeight
    {
        [SerializeField] private BalloonType _type;
        [SerializeField] private float _weight;

        [Tooltip("0 = use the catalog BalloonPrefabEntry.MaxCount for this type.")]
        [SerializeField] private int _maxCountOverride;

        public BalloonTypeWeight(BalloonType type, float weight, int maxCountOverride = 0)
        {
            _type = type;
            _weight = weight;
            _maxCountOverride = maxCountOverride;
        }

        public BalloonType Type => _type;
        public float Weight => _weight;
        public int MaxCountOverride => _maxCountOverride;
    }
}
