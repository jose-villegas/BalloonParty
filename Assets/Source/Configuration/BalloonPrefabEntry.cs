using System;
using BalloonParty.Balloon;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [Serializable]
    public class BalloonPrefabEntry
    {
        [SerializeField] private BalloonLifetimeScope _prefab;
        [SerializeField] private float _weight = 1f;

        [Tooltip("Maximum number of this balloon type allowed on the grid at once. 0 = no limit.")]
        [SerializeField] private int _maxCount;

        [SerializeField] private bool _overrideNudge;
        [SerializeField] private float _nudgeDistanceOverride;
        [SerializeField] private float _nudgeDurationOverride;

        [SerializeField] private bool _overridePopVfx;
        [SerializeField] private ParticleSystem _popVfxPrefab;

        public BalloonLifetimeScope Prefab => _prefab;
        public float Weight => _weight;

        /// <summary>0 means no limit.</summary>
        public int MaxCount => _maxCount;

        /// <summary>Null when override is disabled — falls back to global config default.</summary>
        public float? NudgeDistanceOverride => _overrideNudge ? _nudgeDistanceOverride : null;

        /// <summary>Null when override is disabled — falls back to global config default.</summary>
        public float? NudgeDurationOverride => _overrideNudge ? _nudgeDurationOverride : null;

        /// <summary>Null when override is disabled — view uses default VFX with balloon color.</summary>
        public ParticleSystem PopVfxPrefab => _overridePopVfx ? _popVfxPrefab : null;


        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
