using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Configuration.Balloons;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    [Serializable]
    internal struct BalloonContentEntry : IContentPiece
    {
        [SerializeField] private BalloonType _type;

        [Tooltip("Kept out of every generated level, its tuning kept, until it is ready to ship (art, " +
                 "a terrain it belongs on). Untick to bring it back at its unlock level.")]
        [SerializeField] private bool _withheld;

        [Tooltip("Threat: a harder balloon, paid from the threat budget. Aid: one that helps or pays out " +
                 "(rainbow, silver, gold), paid from the aid budget.")]
        [SerializeField] private ContentRole _role;

        [Tooltip("First level this type can appear at. It's always in for the featured levels from there.")]
        [SerializeField] [Min(1)] private int _unlockLevel;

        [Tooltip("Budget a level spends to include this type. 0 = a staple, in every level from its unlock on.")]
        [SerializeField] [Min(0)] private int _cost;

        [SerializeField] private ContentWeightRamp _weight;

        [Tooltip("Weighted chance of how many of this type the initial board fill may add (X = count, " +
                 "Y = weight). The last keyframe's X caps the type. Empty = uncapped.")]
        [SerializeField] private AnimationCurve _initialCountWeights;

        [Tooltip("Weighted chance of how many of this type each spawn wave may add. Empty = the initial curve.")]
        [SerializeField] private AnimationCurve _waveCountWeights;

        [Tooltip("Guaranteed on the initial board fill of the unlock level, so the player meets the new type.")]
        [SerializeField] [Min(0)] private int _introGuaranteedCount;

        [Tooltip("Guaranteed on the initial board fill of every later level that includes this type.")]
        [SerializeField] [Min(0)] private int _guaranteedInitialCount;

        public BalloonType Type => _type;
        public ContentRole Role => _role;
        public int UnlockLevel => _unlockLevel;
        public bool Withheld => _withheld;
        public int Cost => _cost;
        public bool RequiresGround => false;
        public bool KeptOffWater => _type is BalloonType.Tough or BalloonType.Tougher;
        public ContentWeightRamp Weight => _weight;

        /// <summary>The type's board cap, read the way the level's own weight entry reads it. 0 = uncapped.</summary>
        public int MaxCount => new BalloonTypeWeight(_type, 0f, _initialCountWeights, null, 0).MaxCount;

        internal BalloonContentEntry(BalloonType type, ContentRole role, int unlockLevel, int cost,
            ContentWeightRamp weight, AnimationCurve initialCountWeights = null,
            AnimationCurve waveCountWeights = null, int introGuaranteedCount = 0, int guaranteedInitialCount = 0,
            bool withheld = false)
        {
            _withheld = withheld;
            _type = type;
            _role = role;
            _unlockLevel = unlockLevel;
            _cost = cost;
            _weight = weight;
            _initialCountWeights = initialCountWeights;
            _waveCountWeights = waveCountWeights;
            _introGuaranteedCount = introGuaranteedCount;
            _guaranteedInitialCount = guaranteedInitialCount;
        }

        internal BalloonTypeWeight ToWeight(int level, System.Random rng)
        {
            var guaranteed = level == _unlockLevel ? _introGuaranteedCount : _guaranteedInitialCount;
            return new BalloonTypeWeight(_type, _weight.Roll(level, _unlockLevel, rng),
                _initialCountWeights, _waveCountWeights, guaranteed);
        }
    }
}
