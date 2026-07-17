using System.Collections.Generic;
using BalloonParty.Nudge;
using BalloonParty.Slots.Actor;
using BalloonParty.Slots.Grid;
using UniRx;
using UnityEngine;

namespace BalloonParty.Editor.ShotSolver
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
        public readonly bool DirectBalanceMotion;

        public ShotDynamicActorSnapshot(
            Vector2Int slotIndex, int balancePriority, int maxBalanceSteps, bool directBalanceMotion)
        {
            SlotIndex = slotIndex;
            BalancePriority = balancePriority;
            MaxBalanceSteps = maxBalanceSteps;
            DirectBalanceMotion = directBalanceMotion;
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

    /// <summary>One balance move's linear path, evaluated purely by elapsed time (see
    /// <see cref="ShotSimDynamicActor.EvaluateBalancePosition" />) rather than ticked frame by frame.
    /// Before the first real move, <see cref="From" /> == <see cref="To" /> == the gathered home
    /// position and <see cref="StartTime" /> sits at negative infinity, so evaluation always resolves
    /// to the home position without a branch.</summary>
    internal struct ShotBalanceSegment
    {
        public Vector2 From;
        public Vector2 To;
        public float StartTime;
        public float Duration;
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
    /// <see cref="IWriteableDynamicSlotActor" /> and <see cref="IBalanceInfluence" /> for the real
    /// <c>BalancePlanner</c> to move it exactly as the live balancer would (@ref plan_shot_geometry
    /// §7b). Also carries the balance-motion segment and nudge-impulse list the moving-circle contact
    /// solve reads (§7c). <see cref="WeightBias" /> always returns 0 and
    /// <see cref="OmnidirectionalBalance" /> is always false — the per-type balance-bias/omnidirectional
    /// behaviour some balloon configs opt into isn't in the plan's snapshot list; logged as an
    /// approximation in the ShotSolver README.</summary>
    internal sealed class ShotSimDynamicActor : IWriteableDynamicSlotActor, IBalanceInfluence
    {
        internal const int MaxImpulses = 8; // mirrors BalloonMotionTicker.MaxImpulsesPerView

        internal readonly List<ShotNudgeImpulse> NudgeImpulses = new();

        public ReactiveProperty<Vector2Int> SlotIndex { get; } = new();
        public ReactiveProperty<bool> IsStable { get; } = new(true);
        public int MaxBalanceSteps { get; internal set; }
        public int BalancePriority { get; internal set; }
        public bool DirectBalanceMotion { get; internal set; }
        public bool OmnidirectionalBalance => false;

        /// <summary>Whether this actor also carries collision geometry in the working set (a poppable/
        /// deflectable balloon) — false for a Dynamic-but-excluded actor like an Unbreakable roamer.</summary>
        internal bool IsShotTarget { get; set; }

        internal IReadOnlyList<NudgeOverride> NudgeOverrides { get; set; }
        internal ShotBalanceSegment BalanceSegment { get; private set; }

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
            return 0;
        }

        /// <summary>Resets to the gathered state for a fresh flight — reused across an entire sweep's
        /// worth of angles rather than reallocated (see <see cref="ShotBoardDynamics.ResetForNewFlight" />).</summary>
        internal void ResetTo(Vector2Int homeSlot, Vector2 homePosition)
        {
            SlotIndex.Value = homeSlot;
            IsStable.Value = true;
            BalanceSegment = new ShotBalanceSegment
            {
                From = homePosition,
                To = homePosition,
                StartTime = float.NegativeInfinity,
                Duration = 1f,
            };
            NudgeImpulses.Clear();
        }

        /// <summary>Starts a new segment from wherever the balance position currently sits at
        /// <paramref name="startTime" /> — mirrors <c>BalloonBalancer.StartBalanceTween</c> reading the
        /// view's live (possibly mid-tween) position as the new tween's start, so a pulse that lands
        /// before the previous move finished still animates from the true in-transit position.</summary>
        internal void BeginBalanceMove(float startTime, Vector2 toPosition, float duration)
        {
            BalanceSegment = new ShotBalanceSegment
            {
                From = EvaluateBalancePosition(startTime),
                To = toPosition,
                StartTime = startTime,
                Duration = duration,
            };
        }

        /// <summary>Balance position plus the summed nudge-impulse offset — the sim's full moving
        /// centre (@ref plan_shot_geometry §7c).</summary>
        internal Vector2 EvaluateCenter(float t)
        {
            return EvaluateBalancePosition(t) + EvaluateNudgeOffset(t);
        }

        /// <summary>The balance-only layer <see cref="EvaluateCenter" /> stacks the nudge offset on
        /// top of.</summary>
        internal Vector2 EvaluateBalancePosition(float t)
        {
            var segment = BalanceSegment;
            var duration = segment.Duration <= 0f ? 1f : segment.Duration;
            var localT = Mathf.Clamp01((t - segment.StartTime) / duration);
            return Vector2.Lerp(segment.From, segment.To, localT);
        }

        /// <summary>Constant while a move is in flight (the relative-velocity quadratic's exact term);
        /// zero once it has settled or before the first move ever starts.</summary>
        internal Vector2 EvaluateBalanceVelocity(float t)
        {
            var segment = BalanceSegment;
            var duration = segment.Duration <= 0f ? 1f : segment.Duration;
            var localT = (t - segment.StartTime) / duration;
            if (localT < 0f || localT >= 1f)
            {
                return Vector2.zero;
            }

            return (segment.To - segment.From) / duration;
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
