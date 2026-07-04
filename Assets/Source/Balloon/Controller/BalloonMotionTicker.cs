using System;
using System.Collections.Generic;
using BalloonParty.Balloon.View;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>
    ///     Single driver for the balloon nudge out-and-back (audit 3c), which previously
    ///     allocated a DOTween Sequence (+2 tweeners + closure) per nudged neighbor —
    ///     60–100+ heap objects per bomb shockwave. One Tick walks a flat pooled list and
    ///     calls view setters — the controller owns motion state, the views own their
    ///     transforms. (An idle-bob half replacing the per-balloon stable Animators lived
    ///     here briefly — audit 5e — and was cancelled after in-editor evaluation showed
    ///     no measurable win.)
    ///     Arbitration (the killed-tween semantics this replaces): starting a nudge for a
    ///     view silently replaces its running one; balance moves and despawn must call
    ///     <see cref="CancelNudge"/> explicitly, because a ticker-driven lerp escapes the
    ///     <c>transform.DOKill()</c> that used to cancel the sequence implicitly.
    /// </summary>
    internal sealed class BalloonMotionTicker : ITickable
    {
        private readonly List<NudgeEntry> _nudges = new(32);
        private readonly Stack<NudgeEntry> _nudgePool = new(32);

        public void Tick()
        {
            TickNudges();
        }

        internal void StartNudge(
            IBalloonMotionView view,
            Vector3 slotPosition,
            Vector3 offset,
            float duration,
            Action onComplete)
        {
            CancelNudge(view);

            var entry = _nudgePool.Count > 0 ? _nudgePool.Pop() : new NudgeEntry();
            entry.View = view;
            entry.SlotPosition = slotPosition;
            entry.Offset = offset;
            entry.Duration = Mathf.Max(duration, 0.0001f);
            entry.Elapsed = 0f;
            entry.OnComplete = onComplete;
            _nudges.Add(entry);
        }

        /// <summary>Removes a view's running nudge without completing it.</summary>
        internal void CancelNudge(IBalloonMotionView view)
        {
            for (var i = 0; i < _nudges.Count; i++)
            {
                if (!ReferenceEquals(_nudges[i].View, view))
                {
                    continue;
                }

                RecycleNudge(i);
                return;
            }
        }

        private void TickNudges()
        {
            var deltaTime = Time.deltaTime;

            // Backwards: completions swap-remove, and callbacks may start new nudges —
            // appended entries are not visited until the next tick.
            for (var i = _nudges.Count - 1; i >= 0; i--)
            {
                var entry = _nudges[i];
                entry.Elapsed += deltaTime;
                var progress = Mathf.Clamp01(entry.Elapsed / entry.Duration);

                // Two OutQuad halves — matches the DOMove pair this replaces.
                var reach = progress < 0.5f
                    ? EaseOutQuad(progress * 2f)
                    : 1f - EaseOutQuad((progress - 0.5f) * 2f);
                entry.View.ApplyNudgePosition(entry.SlotPosition + entry.Offset * reach);

                if (progress < 1f)
                {
                    continue;
                }

                var view = entry.View;
                var onComplete = entry.OnComplete;
                RecycleNudge(i);
                view.CompleteNudge(onComplete);
            }
        }

        private void RecycleNudge(int index)
        {
            var entry = _nudges[index];
            entry.View = null;
            entry.OnComplete = null;
            _nudgePool.Push(entry);

            _nudges[index] = _nudges[_nudges.Count - 1];
            _nudges.RemoveAt(_nudges.Count - 1);
        }

        private static float EaseOutQuad(float t)
        {
            return 1f - (1f - t) * (1f - t);
        }

        private sealed class NudgeEntry
        {
            public IBalloonMotionView View;
            public Vector3 SlotPosition;
            public Vector3 Offset;
            public float Duration;
            public float Elapsed;
            public Action OnComplete;
        }
    }
}
