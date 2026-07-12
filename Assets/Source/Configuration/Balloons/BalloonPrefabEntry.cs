using System;
using BalloonParty.Balloon.Type;
using BalloonParty.Balloon.View;
using BalloonParty.Nudge;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class BalloonPrefabEntry
    {
        [SerializeField] private BalloonView _prefab;
        [SerializeField] private BalloonType _balloonType;

        [SerializeField] private NudgeOverride[] _nudgeOverrides;

        [SerializeField] private HitVfxOverride[] _hitVfxOverrides;

        [Tooltip("How many hits this balloon type absorbs before popping. 1 = normal, 2+ = tough, -1 = unbreakable.")]
        [SerializeField] private int _hitsToPop = 1;

        [Tooltip("How many points of the balloon's color are awarded when this balloon pops.")]
        [SerializeField] private int _scoreValue = 1;

        [Tooltip("Relative chance this balloon is chosen to host an item when items are granted. 0 = never.")]
        [SerializeField] private float _itemActivationWeight = 1f;

        [Tooltip("Same-type proximity tendency (squared world distance × this): positive keeps apart (tough), negative clumps together (soap). 0 = off.")]
        [SerializeField] private float _separationBias;

        [Tooltip("Balance-weight bias toward candidates whose diagonal hex neighbours share this balloon's color (per matching neighbour × this) — encourages diagonal same-color lines. Currently honoured by simple colored balloons. 0 = off.")]
        [SerializeField] private float _diagonalColorBias;

        [Tooltip("Max slots this balloon rises per rebalance — lower reads heavier/slower. 0 = unlimited.")]
        [SerializeField] private int _maxBalanceSteps;

        [Tooltip("Intervention order in each rebalance round: higher acts first and wins contested slots (the race). Negative = acts after neutral types.")]
        [SerializeField] private int _balancePriority;

        [Tooltip("A projectile deflecting off this balloon stamps the disturbance field at its slot, radius = BalloonDeflect profile × this (unbreakable ~3 for a heavy jolt, tough 1 for the elastic bounce). 0 = no stamp.")]
        [SerializeField] private float _deflectStampScale;

        [Tooltip("Physical spawn weight: within a spawn wave, heavier types fill the later (lower) lines, entering under lighter ones. 0 = neutral.")]
        [SerializeField] private int _spawnWeight;

        public BalloonView Prefab => _prefab;
        public BalloonType BalloonType => _balloonType;
        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;

        public HitVfxOverride[] HitVfxOverrides => _hitVfxOverrides;

        public int HitsToPop => _hitsToPop;
        public int ScoreValue => _scoreValue;
        public float ItemActivationWeight => _itemActivationWeight;
        public float SeparationBias => _separationBias;
        public float DiagonalColorBias => _diagonalColorBias;
        public int MaxBalanceSteps => _maxBalanceSteps;
        public int BalancePriority => _balancePriority;
        public float DeflectStampScale => _deflectStampScale;
        public int SpawnWeight => _spawnWeight;

        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
