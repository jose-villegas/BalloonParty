using System;
using System.Collections.Generic;
using BalloonParty.Balloon.View;
using BalloonParty.Configuration.Effects;
using BalloonParty.Shared.Disturbance;
using BalloonParty.Shared.Extensions;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    /// Owns balloon view motion in one late pass so writers never fight. Two layers stack on a single
    /// per-view base position:
    /// <list type="bullet">
    /// <item>a balance drive — a Catmull-Rom path traversal (see <see cref="StartBalanceMove"/>) that the
    /// ticker advances itself, replacing the old per-move DOTween DOPath and its allocating closures; and</item>
    /// <item>nudge impulses — stackable transient offsets (see <see cref="AddImpulse"/>).</item>
    /// </list>
    /// When no balance drive is active the base is adopted from whoever last wrote the transform (spawn
    /// path, pool teleport, level-up float-away): the base is detected by comparing this frame's position
    /// against what the ticker wrote last frame, never assumed. An external write during a balance drive
    /// makes the drive yield without finalizing — mirroring a DOPath tween killed mid-flight.
    /// Runs last in the frame as an <see cref="ILateTickable"/>.
    /// </summary>
    internal sealed class BalloonMotionTicker : ILateTickable
    {
        private const int MaxImpulsesPerView = 8;
        private const float AdoptEpsilonSqr = 1e-12f;
        private const float MinDuration = 0.0001f;

        private readonly List<MotionState> _states = new(32);
        private readonly Dictionary<IBalloonMotionView, MotionState> _lookup = new(32);
        private readonly Stack<MotionState> _pool = new(32);

        public void LateTick()
        {
            Advance(Time.deltaTime);
        }

        internal void AddImpulse(IBalloonMotionView view, Vector3 offset, float duration)
        {
            var state = GetOrCreateState(view);

            var impulse = new NudgeImpulse { Offset = offset, Duration = Mathf.Max(duration, MinDuration) };

            if (state.Impulses.Count < MaxImpulsesPerView)
            {
                state.Impulses.Add(impulse);
                return;
            }

            OverwriteMostComplete(state.Impulses, impulse);
        }

        /// <summary>
        /// Drives <paramref name="view"/> along <paramref name="waypoints"/> (waypoint 0 is the current
        /// position) over <paramref name="duration"/> with an OutQuad ease, stamping the disturbance field
        /// along the way. On arrival it invokes <paramref name="onComplete"/> with <paramref name="payload"/>.
        /// Starting a move over an in-flight one replaces it without finalizing; existing nudge impulses
        /// keep stacking on top. The <paramref name="waypoints"/> list is copied, so the caller may reuse
        /// its buffer immediately.
        /// </summary>
        internal void StartBalanceMove(
            IBalloonMotionView view,
            IReadOnlyList<Vector3> waypoints,
            float duration,
            DisturbanceFieldService field,
            StampProfile profile,
            Action<object> onComplete,
            object payload)
        {
            var state = GetOrCreateState(view);

            state.BalanceWaypoints.Clear();
            for (var i = 0; i < waypoints.Count; i++)
            {
                state.BalanceWaypoints.Add(waypoints[i]);
            }

            state.BalanceActive = true;
            state.BalanceElapsed = 0f;
            state.BalanceDuration = Mathf.Max(duration, MinDuration);
            state.BalanceField = field;
            state.BalanceProfile = profile;
            state.BalanceOnComplete = onComplete;
            state.BalancePayload = payload;
            state.BasePosition = waypoints[0];
            state.BalanceLastStamp = waypoints[0];
            state.LastWritten = view.Position;
        }

        /// <summary>Clears a view's motion (balance drive and impulses) without a final write — the caller is despawning/teleporting the view.</summary>
        internal void CancelAll(IBalloonMotionView view)
        {
            if (_lookup.TryGetValue(view, out var state))
            {
                ReleaseStateAt(_states.IndexOf(state));
            }
        }

        /// <summary>
        /// Drops every in-flight balance drive without finalizing (no completion callback), matching a
        /// run reset that kills balance tweens: <c>BalancePathHolder</c> drops all transit state anyway.
        /// A view still carrying nudge impulses keeps its state; a balance-only view is released.
        /// </summary>
        internal void CancelAllBalanceMoves()
        {
            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];
                if (!state.BalanceActive)
                {
                    continue;
                }

                ClearBalanceDrive(state);

                if (state.Impulses.Count == 0)
                {
                    ReleaseStateAt(i);
                }
            }
        }

        // Internal so edit-mode tests can drive the ticker deterministically (Time.deltaTime isn't injectable).
        internal void Advance(float deltaTime)
        {
            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];
                var view = state.View;

                // Someone else moved the transform since our last write (pool teleport, float-away, ...).
                var externallyMoved = (view.Position - state.LastWritten).sqrMagnitude > AdoptEpsilonSqr;

                // An external write during a balance drive means another system claimed the transform —
                // yield the drive without finalizing, exactly as a killed DOPath tween never fires OnComplete.
                if (state.BalanceActive && externallyMoved)
                {
                    ClearBalanceDrive(state);
                }

                if (state.BalanceActive)
                {
                    AdvanceBalance(state, deltaTime);
                }
                else if (externallyMoved)
                {
                    state.BasePosition = view.Position;
                }

                var totalOffset = AdvanceImpulses(state.Impulses, deltaTime);

                if (!state.BalanceActive && state.Impulses.Count == 0)
                {
                    view.Position = state.BasePosition;
                    ReleaseStateAt(i);
                    continue;
                }

                view.Position = state.BasePosition + totalOffset;
                // Store the read-back, not the computed value: the setter round-trips world→local,
                // so under a non-identity parent the transform may hold a value that differs from
                // what we wrote by more than the adoption epsilon — comparing against the read-back
                // keeps reconciliation immune to that float error.
                state.LastWritten = view.Position;
            }
        }

        private MotionState GetOrCreateState(IBalloonMotionView view)
        {
            if (_lookup.TryGetValue(view, out var state))
            {
                return state;
            }

            state = _pool.Count > 0 ? _pool.Pop() : new MotionState();
            state.View = view;
            // First-frame rule: seed both from the view's current position so frame one never snaps.
            state.BasePosition = view.Position;
            state.LastWritten = view.Position;
            _states.Add(state);
            _lookup.Add(view, state);
            return state;
        }

        // Advances the balance drive one step: eased Catmull-Rom position, a distance-gated wake stamp,
        // and the completion callback on arrival. The drive clears before the callback fires so a callback
        // that starts a fresh move on the same view isn't stomped.
        private static void AdvanceBalance(MotionState state, float deltaTime)
        {
            state.BalanceElapsed += deltaTime;
            var progress = Mathf.Clamp01(state.BalanceElapsed / state.BalanceDuration);
            state.BasePosition = EvaluatePath(state.BalanceWaypoints, EaseOutQuad(progress));

            StampBalanceWake(state);

            if (progress < 1f)
            {
                return;
            }

            var onComplete = state.BalanceOnComplete;
            var payload = state.BalancePayload;
            ClearBalanceDrive(state);
            onComplete?.Invoke(payload);
        }

        // Distance-gated wake, sharing DisturbanceTweenExtensions' gate so 120Hz and 60Hz deposit the same
        // stamps for equal travel. Stamps at unit scale: the old OnUpdate scaled radius/strength by the
        // view's localScale, which is 1 for a settled balloon (the norm) — a balloon still scaling in as it
        // balances stamps at full size a touch early. Uses the balance base, not base+impulse, matching the
        // old DOPath OnUpdate (impulses applied later in the frame).
        private static void StampBalanceWake(MotionState state)
        {
            var field = state.BalanceField;
            if (field == null)
            {
                return;
            }

            var pos = state.BasePosition;
            var profile = state.BalanceProfile;
            var spacing = profile.Spacing > 0f ? profile.Spacing : profile.Radius;

            if (!DisturbanceTweenExtensions.TryGateStamp(pos, state.BalanceLastStamp, spacing, out var anchor, out var dir))
            {
                state.BalanceLastStamp = anchor;
                return;
            }

            field.Stamp(pos, profile.Radius, profile.Strength, dir, profile.Duration);
            state.BalanceLastStamp = anchor;
        }

        // Catmull-Rom through the waypoints, uniform per segment. Grid balance steps are one cell each, so
        // equal segments already run at near-constant speed — no arc-length reparameterization needed. Two
        // waypoints collapse to a straight lerp (the extrapolated endpoints stay collinear).
        private static Vector3 EvaluatePath(IReadOnlyList<Vector3> waypoints, float t)
        {
            var count = waypoints.Count;
            if (count < 2)
            {
                return waypoints[0];
            }

            var segmentCount = count - 1;
            var scaled = t * segmentCount;
            var segment = Mathf.Min((int)scaled, segmentCount - 1);
            var u = scaled - segment;

            var p1 = waypoints[segment];
            var p2 = waypoints[segment + 1];
            var p0 = segment > 0 ? waypoints[segment - 1] : 2f * p1 - p2;
            var p3 = segment < segmentCount - 1 ? waypoints[segment + 2] : 2f * p2 - p1;

            return CatmullRom(p0, p1, p2, p3, u);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            var u2 = u * u;
            var u3 = u2 * u;

            return 0.5f * (
                2f * p1
                + (-p0 + p2) * u
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * u3);
        }

        // Advances every impulse, swap-removing completed ones, and returns the summed offset of
        // what remains. Backwards so swap-remove doesn't skip elements.
        private static Vector3 AdvanceImpulses(List<NudgeImpulse> impulses, float deltaTime)
        {
            var totalOffset = Vector3.zero;

            for (var j = impulses.Count - 1; j >= 0; j--)
            {
                var impulse = impulses[j];
                impulse.Elapsed += deltaTime;
                var progress = Mathf.Clamp01(impulse.Elapsed / impulse.Duration);

                if (progress >= 1f)
                {
                    impulses[j] = impulses[^1];
                    impulses.RemoveAt(impulses.Count - 1);
                    continue;
                }

                impulses[j] = impulse;
                totalOffset += impulse.Offset * Reach(progress);
            }

            return totalOffset;
        }

        // Cap reached — overwrite whichever impulse is closest to finishing (silent; shockwave spam cap).
        private static void OverwriteMostComplete(List<NudgeImpulse> impulses, NudgeImpulse replacement)
        {
            var replaceIndex = 0;
            var bestProgress = -1f;

            for (var i = 0; i < impulses.Count; i++)
            {
                var progress = impulses[i].Elapsed / impulses[i].Duration;
                if (progress > bestProgress)
                {
                    bestProgress = progress;
                    replaceIndex = i;
                }
            }

            impulses[replaceIndex] = replacement;
        }

        private static void ClearBalanceDrive(MotionState state)
        {
            state.BalanceActive = false;
            state.BalanceOnComplete = null;
            state.BalancePayload = null;
            state.BalanceField = null;
        }

        private static float Reach(float progress)
        {
            return progress < 0.5f
                ? EaseOutQuad(progress * 2f)
                : 1f - EaseOutQuad((progress - 0.5f) * 2f);
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private void ReleaseStateAt(int index)
        {
            var state = _states[index];
            _lookup.Remove(state.View);

            _states[index] = _states[^1];
            _states.RemoveAt(_states.Count - 1);

            state.View = null;
            state.Impulses.Clear();
            ClearBalanceDrive(state);
            state.BalanceWaypoints.Clear();
            _pool.Push(state);
        }

        private struct NudgeImpulse
        {
            public Vector3 Offset;
            public float Duration;
            public float Elapsed;
        }

        private sealed class MotionState
        {
            public IBalloonMotionView View;
            public Vector3 BasePosition;
            public Vector3 LastWritten;
            public readonly List<NudgeImpulse> Impulses = new(MaxImpulsesPerView);

            public bool BalanceActive;
            public readonly List<Vector3> BalanceWaypoints = new(4);
            public float BalanceElapsed;
            public float BalanceDuration;
            public Vector3 BalanceLastStamp;
            public DisturbanceFieldService BalanceField;
            public StampProfile BalanceProfile;
            public Action<object> BalanceOnComplete;
            public object BalancePayload;
        }
    }
}
