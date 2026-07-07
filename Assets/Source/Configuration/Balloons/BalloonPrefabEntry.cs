using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using BalloonParty.Configuration.Balloons;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class BalloonPrefabEntry : IWeightedEntry
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

        [Tooltip("Relative chance this balloon is chosen to host an item when items are granted. 0 = never.")]
        [SerializeField] private float _itemActivationWeight = 1f;

        [Tooltip("Rainbow only: fraction of ScoreValue granted to each non-current allowed colour (0-1).")]
        [Range(0f, 1f)]
        [SerializeField] private float _spillover;

        public BalloonView Prefab => _prefab;
        public BalloonType BalloonType => _balloonType;
        public float Weight => _weight;

        /// <summary>0 means no limit.</summary>
        public int MaxCount => _maxCount;

        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;

        public HitVfxOverride[] HitVfxOverrides => _hitVfxOverrides;

        public int HitsToPop => _hitsToPop;
        public int ScoreValue => _scoreValue;
        public float ItemActivationWeight => _itemActivationWeight;
        public float Spillover => _spillover;

        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
