using System;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Ranges;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>A grid actor in the catalogue. Actors block the board, so they're paid from the threat budget.</summary>
    [Serializable]
    internal struct GridActorContentEntry : IContentPiece
    {
        [SerializeField] private GridActorType _type;

        [Tooltip("Kept out of every generated level, its tuning kept, until it is ready to ship (art, " +
                 "a terrain it belongs on). Untick to bring it back at its unlock level.")]
        [SerializeField] private bool _withheld;

        [Tooltip("First level this actor can appear at. It's always in for the featured levels from there.")]
        [SerializeField] [Min(1)] private int _unlockLevel;

        [Tooltip("Budget a level spends to include this actor. 0 = a staple, in every level from its unlock on.")]
        [SerializeField] [Min(0)] private int _cost;

        [Tooltip("Slots it takes just after unlock, rolled between Min and Max.")]
        [SerializeField] private RangedInt _count;

        [Tooltip("Slots it takes once the ramp has run, rolled between Min and Max.")]
        [SerializeField] private RangedInt _lateCount;

        [Tooltip("Levels after unlock to move from the early count to the late one. 0 = late count at once.")]
        [SerializeField] [Min(0)] private int _rampLevels;

        [Tooltip("Maximum slots per individual cluster. 0 = no limit. Only used with Cluster placement.")]
        [SerializeField] [Min(0)] private int _maxPerCluster;

        [Tooltip("Only on ground levels: an actor that reads as floating over the sky backdrop (bushes).")]
        [SerializeField] private bool _requiresGround;

        public GridActorType Type => _type;
        public ContentRole Role => ContentRole.Threat;
        public int UnlockLevel => _unlockLevel;
        public bool Withheld => _withheld;
        public int Cost => _cost;
        public bool RequiresGround => _requiresGround;
        public bool KeptOffWater =>
            _requiresGround || _type is GridActorType.Puff or GridActorType.Deflector or GridActorType.Gatekeeper;

        internal GridActorContentEntry(GridActorType type, int unlockLevel, int cost, RangedInt count,
            RangedInt lateCount, int rampLevels, int maxPerCluster = 0, bool withheld = false,
            bool requiresGround = false)
        {
            _withheld = withheld;
            _type = type;
            _unlockLevel = unlockLevel;
            _cost = cost;
            _count = count;
            _lateCount = lateCount;
            _rampLevels = rampLevels;
            _maxPerCluster = maxPerCluster;
            _requiresGround = requiresGround;
        }

        // The level's count range, rolled when the level resolves.
        internal GridActorTypeGate ToGate(int level)
        {
            var progress = ContentWeightRamp.RampProgress(level, _unlockLevel, _rampLevels);
            var min = Mathf.RoundToInt(Mathf.Lerp(_count.Min, _lateCount.Min, progress));
            var max = Mathf.RoundToInt(Mathf.Lerp(_count.Max, _lateCount.Max, progress));
            return new GridActorTypeGate(_type, new RangedInt(min, max, RangeMode.Random), _maxPerCluster);
        }
    }
}
