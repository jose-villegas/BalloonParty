using System;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Presence in a range's array gates one <see cref="GridActorType" /> for that level. A
    ///     <see cref="RangedInt" /> count, not a weight, like <see cref="BalloonTypeWeight" /> — grid
    ///     actors have no competitive weighted draw to feed, so the resolved count is the roll.
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
