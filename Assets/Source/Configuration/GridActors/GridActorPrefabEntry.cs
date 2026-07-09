using System;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;
using BalloonParty.Configuration.GridActors;

namespace BalloonParty.Configuration.GridActors
{
    [Serializable]
    public class GridActorPrefabEntry
    {
        [SerializeField] private GridActorView _prefab;
        [SerializeField] private GridActorType _actorType;
        [SerializeField] private SlotPlacementMode _placementMode;

        [Tooltip("Maximum slots per individual cluster. 0 = no limit. Only used with Cluster placement.")]
        [SerializeField] private int _maxPerCluster = 3;

        [Tooltip("For Gatekeeper: how many hits before the actor is removed.")]
        [SerializeField] private int _hitsToPop = 1;

        public GridActorView Prefab => _prefab;
        public GridActorType ActorType => _actorType;
        public SlotPlacementMode PlacementMode => _placementMode;
        public int MaxPerCluster => _maxPerCluster;
        public int HitsToPop => _hitsToPop;

        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
