using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [Serializable]
    public class BalloonPrefabEntry
    {
        [SerializeField] private BalloonView _prefab;
        [SerializeField] private BalloonType _balloonType;
        [SerializeField] private float _weight = 1f;

        [Tooltip("Maximum number of this balloon type allowed on the grid at once. 0 = no limit.")]
        [SerializeField] private int _maxCount;

        [SerializeField] private NudgeOverride[] _nudgeOverrides;

        [SerializeField] private HitVfxOverride[] _hitVfxOverrides;

        [Tooltip("How many hits this balloon type absorbs before popping. 1 = normal, 2+ = tough, -1 = unbreakable.")]
        [SerializeField] private int _hitsToPop = 1;

        [Tooltip("How many points of the balloon's color are awarded when this balloon pops.")]
        [SerializeField] private int _scoreValue = 1;

        public BalloonView Prefab => _prefab;
        public BalloonType BalloonType => _balloonType;
        public float Weight => _weight;

        /// <summary>0 means no limit.</summary>
        public int MaxCount => _maxCount;

        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;

        public HitVfxOverride[] HitVfxOverrides => _hitVfxOverrides;

        public int HitsToPop => _hitsToPop;
        public int ScoreValue => _scoreValue;

        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
