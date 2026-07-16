using System.Collections.Generic;
using BalloonParty.Balloon.View;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    /// Applies nudge impulses as a stackable offset on top of a base position owned by whichever
    /// system last wrote the view's transform (spawn path, balance DOPath, pool teleport). Runs
    /// last in the frame as an <see cref="ILateTickable"/> so it never fights another writer: the
    /// base is detected by comparing this frame's position against what was written last frame,
    /// never assumed or pushed by callers. See PLAN-NudgeLayeredMotion for the model.
    /// </summary>
    internal sealed class BalloonMotionTicker : ILateTickable
    {
        private const int MaxImpulsesPerView = 8;
        private const float AdoptEpsilonSqr = 1e-12f;

        private readonly List<NudgeState> _states = new(32);
        private readonly Dictionary<IBalloonMotionView, NudgeState> _lookup = new(32);
        private readonly Stack<NudgeState> _pool = new(32);

        public void LateTick()
        {
            Advance(Time.deltaTime);
        }

        internal void AddImpulse(IBalloonMotionView view, Vector3 offset, float duration)
        {
            if (!_lookup.TryGetValue(view, out var state))
            {
                state = _pool.Count > 0 ? _pool.Pop() : new NudgeState();
                state.View = view;
                // First-frame rule: seed both from the view's current position so frame one never snaps.
                state.BasePosition = view.Position;
                state.LastWritten = view.Position;
                _states.Add(state);
                _lookup.Add(view, state);
            }

            var impulse = new NudgeImpulse { Offset = offset, Duration = Mathf.Max(duration, 0.0001f) };

            if (state.Impulses.Count < MaxImpulsesPerView)
            {
                state.Impulses.Add(impulse);
                return;
            }

            OverwriteMostComplete(state.Impulses, impulse);
        }

        /// <summary>Clears a view's impulses and drops its state without a final write — the caller is despawning/teleporting the view.</summary>
        internal void CancelAll(IBalloonMotionView view)
        {
            if (_lookup.TryGetValue(view, out var state))
            {
                ReleaseStateAt(_states.IndexOf(state));
            }
        }

        // Internal so edit-mode tests can drive the ticker deterministically (Time.deltaTime isn't injectable).
        internal void Advance(float deltaTime)
        {
            for (var i = _states.Count - 1; i >= 0; i--)
            {
                var state = _states[i];
                var view = state.View;

                // Someone else moved the transform since our last write (DOTween, pool teleport, ...) —
                // adopt it as the new base rather than fighting it.
                if ((view.Position - state.LastWritten).sqrMagnitude > AdoptEpsilonSqr)
                {
                    state.BasePosition = view.Position;
                }

                var totalOffset = AdvanceImpulses(state.Impulses, deltaTime);

                if (state.Impulses.Count == 0)
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
            _pool.Push(state);
        }

        private struct NudgeImpulse
        {
            public Vector3 Offset;
            public float Duration;
            public float Elapsed;
        }

        private sealed class NudgeState
        {
            public IBalloonMotionView View;
            public Vector3 BasePosition;
            public Vector3 LastWritten;
            public readonly List<NudgeImpulse> Impulses = new(MaxImpulsesPerView);
        }
    }
}
