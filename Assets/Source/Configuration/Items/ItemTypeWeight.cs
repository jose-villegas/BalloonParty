using System;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Configuration.Items
{
    /// <summary>Item counterpart of <see cref="BalloonTypeWeight" /> — same gate/override semantics.</summary>
    [Serializable]
    public struct ItemTypeWeight
    {
        [SerializeField] private ItemType _type;
        [SerializeField] private float _weight;

        [Tooltip("0 = use the catalog ItemSettings.MaximumAllowed for this type.")]
        [SerializeField] private int _maximumAllowedOverride;

        [Tooltip("Guaranteed minimum of this item placed during the initial board fill, before weighted " +
                 "picks run. 0 = no guarantee (purely weight-driven). Use for tutorialization.")]
        [SerializeField] private int _guaranteedInitialCount;

        public ItemType Type => _type;
        public float Weight => _weight;
        public int MaximumAllowedOverride => _maximumAllowedOverride;
        public int GuaranteedInitialCount => _guaranteedInitialCount;

        public ItemTypeWeight(ItemType type, float weight, int maximumAllowedOverride = 0, int guaranteedInitialCount = 0)
        {
            _type = type;
            _weight = weight;
            _maximumAllowedOverride = maximumAllowedOverride;
            _guaranteedInitialCount = guaranteedInitialCount;
        }
    }
}
