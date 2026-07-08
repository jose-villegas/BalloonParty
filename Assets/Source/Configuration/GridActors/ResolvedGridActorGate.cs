using System;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;
using BalloonParty.Configuration.GridActors;

namespace BalloonParty.Configuration.GridActors
{
    /// <summary>Resolved counterpart of <see cref="GridActorTypeGate" /> — <see cref="Count" /> is
    /// an already-rolled plain <c>int</c>, not an unresolved <see cref="RangedInt" />.</summary>
    [Serializable]
    public struct ResolvedGridActorGate
    {
        [SerializeField] private GridActorType _type;
        [SerializeField] private int _count;

        public GridActorType Type => _type;
        public int Count => _count;

        public ResolvedGridActorGate(GridActorType type, int count)
        {
            _type = type;
            _count = count;
        }
    }
}
