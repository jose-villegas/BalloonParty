using System;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     An exact-level overlay ("level 10 is special") authored beside the ranges. Resolve
    ///     specificity: an exact <see cref="Level" /> match wins over the containing range; ranges
    ///     stay contiguous and never know customs exist. A custom is just a single-level range —
    ///     it authors a <see cref="RangedLevelParameters" /> (typically with fixed min==max spans)
    ///     resolved at position 0, so there is one authoring shape for both ranges and customs.
    /// </summary>
    [Serializable]
    public struct CustomLevelEntry
    {
        [SerializeField] private int _level;
        [SerializeField] private RangedLevelParameters _parameters;

        public int Level => _level;
        public RangedLevelParameters Parameters => _parameters;
    }
}
