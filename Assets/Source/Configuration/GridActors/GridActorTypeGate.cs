using System;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;
using BalloonParty.Configuration.GridActors;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Configuration.GridActors
{
    /// <summary>
    ///     Presence in a range's array gates one <see cref="GridActorType" /> for that level.
    /// </summary>
    [Serializable]
    public struct GridActorTypeGate
    {
        [SerializeField] private GridActorType _type;
        [SerializeField] private RangedInt _count;

        [Tooltip("Maximum slots per individual cluster. 0 = no limit. Only used with Cluster placement.")]
        [SerializeField] private int _maxPerCluster;

        public GridActorType Type => _type;
        public RangedInt Count => _count;
        public int MaxPerCluster => _maxPerCluster;

        public GridActorTypeGate(GridActorType type, RangedInt count, int maxPerCluster = 0)
        {
            _type = type;
            _count = count;
            _maxPerCluster = maxPerCluster;
        }
    }
}
