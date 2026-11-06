using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>The item-count distributions in force from <see cref="FromLevel" /> on.</summary>
    [Serializable]
    internal struct ItemCountStep : ILevelStep
    {
        [SerializeField] [Min(1)] private int _fromLevel;

        [Tooltip("Weighted distribution for how many items to seed on the initial board fill, rolled once. " +
                 "X = item count (0, 1, 2…), Y = weight. Empty = none.")]
        [SerializeField] private AnimationCurve _initialItemCountWeights;

        [Tooltip("Weighted distribution for how many items to grant on each cadence wave. X = item count, " +
                 "Y = weight. Capped by the eligible new balloons.")]
        [SerializeField] private AnimationCurve _itemCountWeights;

        public int FromLevel => _fromLevel;
        public AnimationCurve InitialItemCountWeights => _initialItemCountWeights;
        public AnimationCurve ItemCountWeights => _itemCountWeights;

        internal ItemCountStep(int fromLevel, AnimationCurve initialItemCountWeights, AnimationCurve itemCountWeights)
        {
            _fromLevel = fromLevel;
            _initialItemCountWeights = initialItemCountWeights;
            _itemCountWeights = itemCountWeights;
        }
    }
}
