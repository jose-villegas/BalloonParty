using System;
using BalloonParty.Configuration.Items;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>An item in the catalogue. Items help the player, so they're always paid from the aid budget.</summary>
    [Serializable]
    internal struct ItemContentEntry : IContentPiece
    {
        [SerializeField] private ItemType _type;

        [Tooltip("Kept out of every generated level, its tuning kept, until it is ready to ship (art, " +
                 "a terrain it belongs on). Untick to bring it back at its unlock level.")]
        [SerializeField] private bool _withheld;

        [Tooltip("First level this item can appear at. It's always in for the featured levels from there.")]
        [SerializeField] [Min(1)] private int _unlockLevel;

        [Tooltip("Budget a level spends to include this item. 0 = a staple, in every level from its unlock on.")]
        [SerializeField] [Min(0)] private int _cost;

        [SerializeField] private ContentWeightRamp _weight;

        [Tooltip("0 = use the item catalogue's MaximumAllowed for this type.")]
        [SerializeField] [Min(0)] private int _maximumAllowedOverride;

        [Tooltip("Guaranteed on the initial board fill of the unlock level, so the player meets the new item. " +
                 "Raises that level's maximum to fit.")]
        [SerializeField] [Min(0)] private int _introGuaranteedCount;

        [Tooltip("Guaranteed on the initial board fill of every later level that includes this item.")]
        [SerializeField] [Min(0)] private int _guaranteedInitialCount;

        public ItemType Type => _type;
        public ContentRole Role => ContentRole.Aid;
        public int UnlockLevel => _unlockLevel;
        public bool Withheld => _withheld;
        public int Cost => _cost;
        public ContentWeightRamp Weight => _weight;

        internal ItemContentEntry(ItemType type, int unlockLevel, int cost, ContentWeightRamp weight,
            int maximumAllowedOverride = 0, int introGuaranteedCount = 0, int guaranteedInitialCount = 0,
            bool withheld = false)
        {
            _withheld = withheld;
            _type = type;
            _unlockLevel = unlockLevel;
            _cost = cost;
            _weight = weight;
            _maximumAllowedOverride = maximumAllowedOverride;
            _introGuaranteedCount = introGuaranteedCount;
            _guaranteedInitialCount = guaranteedInitialCount;
        }

        internal ItemTypeWeight ToWeight(int level, System.Random rng)
        {
            var isIntro = level == _unlockLevel;
            var guaranteed = isIntro ? _introGuaranteedCount : _guaranteedInitialCount;

            // An override below the intro guarantee would cap it; the catalogue's own maximum (override 0)
            // is out of reach here, so it still caps.
            var maximum = isIntro && _maximumAllowedOverride > 0
                ? Mathf.Max(_maximumAllowedOverride, _introGuaranteedCount)
                : _maximumAllowedOverride;

            return new ItemTypeWeight(_type, _weight.Roll(level, _unlockLevel, rng), maximum, guaranteed);
        }
    }
}
