using System;
using BalloonParty.Balloon;
using BalloonParty.Nudge;
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

        [SerializeField] private NudgeOverride[] _nudgeOverrides;

        [SerializeField] private bool _overridePopVfx;
        [SerializeField] private ParticleSystem _popVfxPrefab;

        [Tooltip("Whether balloons of this type can receive items. Disable for tough/unbreakable types.")]
        [SerializeField] private bool _canHoldItem = true;

        [Tooltip("How many hits this balloon type absorbs before popping. 1 = normal, 2+ = tough, -1 = unbreakable.")]
        [SerializeField] private int _hitsToPop = 1;

        public BalloonLifetimeScope Prefab => _prefab;
        public float Weight => _weight;

        /// <summary>0 means no limit.</summary>
        public int MaxCount => _maxCount;

        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;

        /// <summary>Null when override is disabled — view uses default VFX with balloon color.</summary>
        public ParticleSystem PopVfxPrefab => _overridePopVfx ? _popVfxPrefab : null;

        public bool CanHoldItem => _canHoldItem;
        public int HitsToPop => _hitsToPop;


        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
