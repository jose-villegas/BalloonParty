using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     An exact-level overlay ("level 10 is special") authored beside the ranges. Resolve
    ///     specificity: an exact <see cref="Level" /> match wins over the containing range; ranges
    ///     stay contiguous and never know customs exist. v1 is full-block authoring — no
    ///     inherit-from-range cascade.
    /// </summary>
    [Serializable]
    public struct CustomLevelEntry
    {
        [SerializeField] private int _level;
        [SerializeField] private LevelParameters _parameters;

        public int Level => _level;
        public LevelParameters Parameters => _parameters;
    }
}
