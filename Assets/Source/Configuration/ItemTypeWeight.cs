using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>Item counterpart of <see cref="BalloonTypeWeight" /> — same gate/override semantics.</summary>
    [Serializable]
    public struct ItemTypeWeight
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private float _weight;

        [Tooltip("0 = use the catalog ItemSettings.MaximumAllowed for this type.")]
        [SerializeField] private int _maximumAllowedOverride;

        public ItemTypeWeight(ItemType type, float weight, int maximumAllowedOverride = 0)
        {
            _type = type;
            _weight = weight;
            _maximumAllowedOverride = maximumAllowedOverride;
        }

        public ItemType Type => _type;
        public float Weight => _weight;
        public int MaximumAllowedOverride => _maximumAllowedOverride;
    }
}
