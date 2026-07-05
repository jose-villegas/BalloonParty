using System;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Presence in a range's array is the gate for one <see cref="GridActorType" /> — absent
    ///     means that type cannot spawn this level. Unlike <see cref="BalloonTypeWeight" /> this
    ///     carries a <see cref="RangedInt" /> count rather than a weight: today's
    ///     <c>StaticActorSpawner</c> rolls a count per catalog entry independently (no competitive
    ///     weighted draw between grid-actor types), so there's nothing for a weight to compete
    ///     against — the resolved count *is* the roll, resolved once per level.
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
