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

        [Tooltip("How the balloon goes when it pops: off, it vanishes at once; on, it swells and shrinks away while its pop effect plays.")]
        [SerializeField] private PopPulse _popPulse = new();

        [Tooltip("How many hits this balloon type absorbs before popping. 1 = normal, 2+ = tough, -1 = unbreakable.")]
        [SerializeField] private int _hitsToPop = 1;

        [Tooltip("How many points of the balloon's color are awarded when this balloon pops.")]
        [SerializeField] private int _scoreValue = 1;

        [Tooltip("Relative chance this balloon is chosen to host an item when items are granted. 0 = never.")]
        [SerializeField] private float _itemActivationWeight = 1f;

        [Tooltip("Per-type balance-weight bias strength. Each balloon type interprets this differently: colored balloons bias toward diagonal same-color neighbours, tough balloons bias toward straight-line (wall) formations. 0 = off.")]
        [SerializeField] private float _balanceBias;

        [Tooltip("Max slots this balloon rises per balance window (a wave's sweeps, or each rebalance pulse) — lower reads heavier/slower. 0 = unlimited.")]
        [SerializeField] private int _maxBalanceSteps;

        [Tooltip("Travel speed (world units/sec) for this type's spawn entry and balance moves — heavier types author a lower value to move slower. 0 = use the config's Default Balloon Move Speed.")]
        [SerializeField] [Min(0f)] private float _moveSpeed;

        [Tooltip("Intervention order in each rebalance round: higher acts first and wins contested slots (the race). Negative = acts after neutral types.")]
        [SerializeField] private int _balancePriority;

        [Tooltip("Heavy movers animate straight from start to final slot instead of touring every resolve waypoint. Author true for Unbreakable.")]
        [SerializeField] private bool _directBalanceMotion;

        [Tooltip("When true, side and down neighbours are valid balance candidates even without a shove — the actor drifts freely in all directions, steered only by its WeightBias (e.g. soap clusters clumping).")]
        [SerializeField] private bool _omnidirectionalBalance;

        [Tooltip("A projectile deflecting off this balloon stamps the disturbance field at its slot, radius = BalloonDeflect profile × this (unbreakable ~3 for a heavy jolt, tough 1 for the elastic bounce). 0 = no stamp.")]
        [SerializeField] private float _deflectStampScale;

        [Tooltip("Physical spawn weight: heavier types take the lower slots, entering under lighter ones (across the initial fill's lines, and across the columns of a line). 0 = neutral.")]
        [SerializeField] private int _spawnWeight;

        [Tooltip("How much likelier a level's mix picks this balloon while the player is in need: its level weight " +
                 "times (1 + this × need), need running 0 to 1 toward the last heart. For the balloons that help, " +
                 "such as Rainbow. 0 = need never changes its odds.")]
        [SerializeField] [Min(0f)] private float _needWeightBoost;

        public BalloonView Prefab => _prefab;
        public BalloonType BalloonType => _balloonType;
        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;

        public HitVfxOverride[] HitVfxOverrides => _hitVfxOverrides;
        public PopPulse PopPulse => _popPulse;

        public int HitsToPop => _hitsToPop;
        public int ScoreValue => _scoreValue;
        public float ItemActivationWeight => _itemActivationWeight;
        public float BalanceBias => _balanceBias;
        public int MaxBalanceSteps => _maxBalanceSteps;
        public float MoveSpeed => _moveSpeed;
        public int BalancePriority => _balancePriority;
        public bool DirectBalanceMotion => _directBalanceMotion;
        public bool OmnidirectionalBalance => _omnidirectionalBalance;
        public float DeflectStampScale => _deflectStampScale;
        public int SpawnWeight => _spawnWeight;
        public float NeedWeightBoost => _needWeightBoost;

        /// <summary>Derived from the prefab's GameObject name — no manual key needed.</summary>
        public string PoolKey => _prefab != null ? _prefab.name : string.Empty;
    }
}
