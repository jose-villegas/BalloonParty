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

        public GridActorTypeGate(GridActorType type, RangedInt count)
        {
            _type = type;
            _count = count;
        }

        public GridActorType Type => _type;
        public RangedInt Count => _count;
    }
}
