using System;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [Serializable]
    public class GridActorPrefabEntry : IWeightedEntry
    {
        [SerializeField] private GridActorView _prefab;
        [SerializeField] private GridActorType _actorType;
        [SerializeField] private SlotPlacementMode _placementMode;
        [SerializeField] private float _weight = 1f;

        [Tooltip("Maximum number of this actor type allowed on the grid at once. 0 = no limit.")]
        [SerializeField] private int _maxCount;

        [Tooltip("For Gatekeeper: how many hits before the actor is removed.")]
        [SerializeField] private int _hitsToPop = 1;

        public GridActorView Prefab => _prefab;
        public GridActorType ActorType => _actorType;
        public SlotPlacementMode PlacementMode => _placementMode;
        public float Weight => _weight;
        public int MaxCount => _maxCount;
        public int HitsToPop => _hitsToPop;

        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
