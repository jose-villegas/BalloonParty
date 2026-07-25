using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Shared.Extensions;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;

namespace BalloonParty.Solver
{
    /// <summary>Gathered occupant of a grid slot that is a dynamic (balanceable) actor but NOT a
    /// poppable/deflectable shot target — e.g. an Unbreakable roamer (durable+scored but excluded by
    /// the existing target filter). Enough to seed <see cref="ShotBoardDynamics" />'s grid so balance
    /// geometry (gaps, blockers) stays correct; carries no collision geometry, matching the existing
    /// static sim's scope (see the ShotSolver README's rule-mirroring section).</summary>
    internal readonly struct ShotDynamicActorSnapshot
    {
        public readonly Vector2Int SlotIndex;
        public readonly int BalancePriority;
        public readonly int MaxBalanceSteps;
        public readonly float MoveSpeed;
        public readonly bool DirectBalanceMotion;
        public readonly bool Omnidirectional;
        public readonly string ColorId;
        public readonly BalanceBiasKind BiasKind;
        public readonly float BiasValue;
        public readonly int BiasTypeId;

        public ShotDynamicActorSnapshot(
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, float moveSpeed,
            bool directBalanceMotion, bool omnidirectional = false, string colorId = "",
            BalanceBiasKind biasKind = BalanceBiasKind.None, float biasValue = 0f, int biasTypeId = 0)
        {
            SlotIndex = slotIndex;
            BalancePriority = balancePriority;
            MaxBalanceSteps = maxBalanceSteps;
            MoveSpeed = moveSpeed;
            DirectBalanceMotion = directBalanceMotion;
            Omnidirectional = omnidirectional;
            ColorId = colorId;
            BiasKind = biasKind;
            BiasValue = biasValue;
            BiasTypeId = biasTypeId;
        }
    }

    /// <summary>Gathered static (non-balanceable) grid occupant — occupies a slot only.</summary>
    internal readonly struct ShotStaticActorSnapshot
    {
        public readonly Vector2Int SlotIndex;

        public ShotStaticActorSnapshot(Vector2Int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    /// <summary>One nudge impulse, evaluated analytically at any time via <see cref="ShotMotionMath.Reach" />
    /// instead of decayed frame by frame — mirrors <c>BalloonMotionTicker</c>'s impulse record.</summary>
    internal struct ShotNudgeImpulse
    {
        public Vector2 Offset;
        public float StartTime;
        public float Duration;
    }

    /// <summary>Stub grid actor standing in for a live balloon in the dynamic-board sim — enough of
    /// <see cref="IWriteableDynamicSlotActor" />, <see cref="IBalanceInfluence" /> and
    /// <see cref="IBalanceBiasSource" /> for the real <c>BalancePlanner</c> to move it exactly as the
    /// live balancer would (@ref plan_shot_geometry §7b), including the gathered actor's per-type
    /// balance-bias/omnidirectional behaviour (@ref plan_shot_solver_accuracy §3 "Weight-bias
    /// sharing" — <see cref="WeightBias" /> runs the identical shared formula a live model would).
    /// Also carries the balance-motion segment and nudge-impulse list the moving-circle contact
    /// solve reads (§7c).</summary>
    internal sealed class ShotSimDynamicActor : IWriteableDynamicSlotActor, IBalanceInfluence, IBalanceBiasSource
    {
        internal const int MaxImpulses = 8; // mirrors BalloonMotionTicker.MaxImpulsesPerView

        // Start position + up to 8 chained hops within one pulse — the planner rarely produces more
        // than 2-3 for a single actor before its step budget or the board stops it.
        private const int MaxWaypoints = 9;

        internal readonly List<ShotNudgeImpulse> NudgeImpulses = new();

        private readonly Vector2[] _waypoints = new Vector2[MaxWaypoints];
        private readonly float[] _cumulativeLengths = new float[MaxWaypoints];

        private int _waypointCount;
        private float _pathLength;
        private float _segmentStartTime = float.NegativeInfinity;
        private float _segmentDuration = 1f;

        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);
        public int MaxBalanceSteps { get; internal set; }
        public float MoveSpeed { get; internal set; }
        public int BalancePriority { get; internal set; }
        public bool DirectBalanceMotion { get; internal set; }
        public bool OmnidirectionalBalance { get; internal set; }

        // IBalanceBiasSource: gathered from the live actor at CollectBoard time (ShotBoardGather) and
        // plumbed through the actor's BalanceProfile (ShotBoardDynamics) — empty/None/0 for a stub with
        // no bias strategy, exactly mirroring BalloonModelBase's default.
        public string ColorId { get; internal set; } = "";
        public int BiasTypeId { get; internal set; }
        public BalanceBiasKind BiasKind { get; internal set; }
        public float BiasValue { get; internal set; }

        /// <summary>Whether this actor also carries collision geometry in the working set (a poppable/
        /// deflectable balloon) — false for a Dynamic-but-excluded actor like an Unbreakable roamer.</summary>
        internal bool IsShotTarget { get; set; }

        internal IReadOnlyList<NudgeOverride> NudgeOverrides { get; set; }

        SlotActorKind ISlotActor.Kind => SlotActorKind.Dynamic;
        Vector2Int ISlotActor.SlotIndex => SlotIndex.Value;
        IReadOnlyReactiveProperty<bool> IDynamicSlotActor.IsStable => IsStable;
        IReadOnlyReactiveProperty<Vector2Int> IDynamicSlotActor.SlotIndex => SlotIndex;

        Vector2Int IWriteableSlotActor.SlotIndex
        {
            get => SlotIndex.Value;
            set => SlotIndex.Value = value;
        }

        public int WeightBias(SlotGrid grid, Vector2Int candidate)
        {
            return this.Evaluate(BiasKind, grid, candidate, BiasValue);
        }

        /// <summary>Resets to the gathered state for a fresh flight — reused across an entire sweep's
        /// worth of angles rather than reallocated (see <see cref="ShotBoardDynamics.ResetForNewFlight" />).</summary>
        internal void ResetTo(Vector2Int homeSlot, Vector2 homePosition)
        {
            SlotIndex.Value = homeSlot;
            IsStable.Value = true;
            _waypoints[0] = homePosition;
            _cumulativeLengths[0] = 0f;
            _waypointCount = 1;
            _pathLength = 0f;
            _segmentStartTime = float.NegativeInfinity;
            _segmentDuration = 1f;
            NudgeImpulses.Clear();
        }

        /// <summary>Starts (or extends) the balance path for a pulse. A NEW pulse starts from the FULL
        /// centre — balance position plus the live nudge offset — because that is what the game does:
        /// <c>BalloonBalancer.StartBalanceTween</c> seeds waypoint 0 with the view's current position
        /// (wobble included), and <c>BalloonMotionTicker</c> then adopts each tween write as the new
        /// base and re-adds the CURRENT impulse offset on top — so a tween starting mid-wobble briefly
        /// double-carries the start offset, converging to lattice+impulses as the path proceeds. A
        /// repeat call from the SAME pulse chains the hop as an extra waypoint, mirroring
        /// <c>RecordPath</c> building the multi-waypoint DOPath — except for direct movers, whose live
        /// tween skips intermediates (<c>FinalWaypointBuffer</c>), so the last waypoint is overwritten
        /// instead.</summary>
        internal void BeginBalanceMove(float startTime, Vector2 toPosition)
        {
            var samePulse = startTime == _segmentStartTime;
            if (!samePulse)
            {
                _waypoints[0] = EvaluateCenter(startTime);
                _waypointCount = 1;
                _segmentStartTime = startTime;
            }

            if (samePulse && (DirectBalanceMotion || _waypointCount >= MaxWaypoints))
            {
                _waypoints[_waypointCount - 1] = toPosition;
            }
            else
            {
                _waypoints[_waypointCount] = toPosition;
                _waypointCount++;
            }

            _cumulativeLengths[0] = 0f;
            for (var i = 1; i < _waypointCount; i++)
            {
                _cumulativeLengths[i] =
                    _cumulativeLengths[i - 1] + Vector2.Distance(_waypoints[i - 1], _waypoints[i]);
            }

            _pathLength = _cumulativeLengths[_waypointCount - 1];

            // Distance ÷ speed, recomputed as chained hops extend the path so the whole pulse settles at
            // a constant speed — mirrors the live BalloonBalancer's speed-based duration.
            _segmentDuration = Mathf.Max(_pathLength / Mathf.Max(MoveSpeed, 0.0001f), 0.0001f);
        }

        /// <summary>Balance position plus the summed nudge-impulse offset — the sim's full moving
        /// centre (@ref plan_shot_geometry §7c).</summary>
        internal Vector2 EvaluateCenter(float t)
        {
            return EvaluateBalancePosition(t) + EvaluateNudgeOffset(t);
        }

        /// <summary>The balance-only layer <see cref="EvaluateCenter" /> stacks the nudge offset on
        /// top of: OutQuad-eased progress (the project's DOTween default ease — DOTweenSettings.asset)
        /// along the waypoint polyline by ARC LENGTH, mirroring DOPath's constant-speed path
        /// percentage. Catmull-Rom's corner rounding is the one thing not reproduced (README).</summary>
        internal Vector2 EvaluateBalancePosition(float t)
        {
            if (_pathLength <= 0f)
            {
                return _waypoints[_waypointCount - 1];
            }

            var localT = Mathf.Clamp01((t - _segmentStartTime) / _segmentDuration);
            var arc = EaseOutQuad(localT) * _pathLength;
            var segmentIndex = ArcSegmentIndex(arc);
            var segmentStart = _cumulativeLengths[segmentIndex - 1];
            var segmentLength = _cumulativeLengths[segmentIndex] - segmentStart;
            var segmentT = segmentLength <= 0f ? 1f : (arc - segmentStart) / segmentLength;
            return Vector2.Lerp(_waypoints[segmentIndex - 1], _waypoints[segmentIndex], segmentT);
        }

        /// <summary>Instantaneous velocity of the eased polyline motion — the moving-circle solve's
        /// linearization term; the two-pass refinement absorbs the easing's curvature. Zero once
        /// settled or before the first move ever starts.</summary>
        internal Vector2 EvaluateBalanceVelocity(float t)
        {
            var localT = (t - _segmentStartTime) / _segmentDuration;
            if (localT < 0f || localT >= 1f || _pathLength <= 0f)
            {
                return Vector2.zero;
            }

            var arc = EaseOutQuad(localT) * _pathLength;
            var segmentIndex = ArcSegmentIndex(arc);
            var segmentStart = _cumulativeLengths[segmentIndex - 1];
            var segmentLength = _cumulativeLengths[segmentIndex] - segmentStart;
            if (segmentLength <= 0f)
            {
                return Vector2.zero;
            }

            var direction = (_waypoints[segmentIndex] - _waypoints[segmentIndex - 1]) / segmentLength;

            // d(arc)/dt = pathLength x ease'(localT)/duration, with OutQuad' = 2(1 - t).
            return direction * (_pathLength * 2f * (1f - localT) / _segmentDuration);
        }

        private Vector2 EvaluateNudgeOffset(float t)
        {
            var total = Vector2.zero;
            for (var i = 0; i < NudgeImpulses.Count; i++)
            {
                var impulse = NudgeImpulses[i];
                var progress = Mathf.Clamp01((t - impulse.StartTime) / impulse.Duration);
                total += impulse.Offset * ShotMotionMath.Reach(progress);
            }

            return total;
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        // First waypoint index whose cumulative length covers the queried arc distance.
        private int ArcSegmentIndex(float arc)
        {
            for (var i = 1; i < _waypointCount; i++)
            {
                if (arc <= _cumulativeLengths[i])
                {
                    return i;
                }
            }

            return _waypointCount - 1;
        }
    }

    /// <summary>Minimal non-dynamic occupant — occupies a grid slot for balance geometry (blocking or
    /// unlocking candidate destinations) but is never moved by <c>BalancePlanner</c>, since it does not
    /// implement <see cref="IWriteableDynamicSlotActor" /> — mirroring every non-balloon grid archetype
    /// in the live game (obstacles, gatekeepers, deflectors, absorbers are all
    /// <see cref="SlotActorKind.Static" />).</summary>
    internal sealed class ShotSimStaticActor : IWriteableSlotActor
    {
        public Vector2Int SlotIndex { get; set; }
        public SlotActorKind Kind => SlotActorKind.Static;
    }
}
