using System;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     The resolved counterpart of <see cref="GridActorTypeGate" /> — same presence-is-the-gate
    ///     semantics, but <see cref="Count" /> is a plain already-rolled value instead of an
    ///     unresolved <see cref="RangedInt" />. Mirrors how <see cref="LevelParameters.SpawnLines" />
    ///     is a plain <c>int</c> while <see cref="RangedLevelParameters.SpawnLines" /> is a
    ///     <see cref="RangedInt" />.
    /// </summary>
    [Serializable]
    public struct ResolvedGridActorGate
    {
        [SerializeField] private GridActorType _type;
        [SerializeField] private int _count;

        public ResolvedGridActorGate(GridActorType type, int count)
        {
            _type = type;
            _count = count;
        }

        public GridActorType Type => _type;
        public int Count => _count;
    }
}
