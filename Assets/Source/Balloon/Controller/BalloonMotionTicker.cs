using System;
using System.Collections.Generic;
using BalloonParty.Balloon.View;
using UnityEngine;
using VContainer.Unity;

namespace BalloonParty.Balloon.Controller
{
    /// <summary>Drives the balloon nudge out-and-back; balance moves and despawn must call <see cref="CancelNudge"/> explicitly since a ticker-driven lerp escapes <c>transform.DOKill()</c>.</summary>
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
            Vector3 startPosition,
            Vector3 returnPosition,
            Vector3 offset,
            float duration,
            Action onComplete)
        {
            var (previousComplete, previousReturn) = CancelNudgeInternal(view);

            // Inherit any deferred balance callback from the nudge being replaced — it would be
            // lost when the old entry is recycled, leaving the balloon stuck at the wrong position.
            onComplete ??= previousComplete;

            // Inherit the return position from the cancelled nudge to prevent drift accumulation
            // when nudges are rapidly replaced mid-flight.
            if (previousReturn.HasValue)
            {
                returnPosition = previousReturn.Value;
            }

            Debug.Log($"[Nudge] MotionTicker.StartNudge: offset={offset.magnitude:F3} dur={duration:F3}" +
                      (previousComplete != null ? " (inherited deferred)" : ""));

            var entry = _nudgePool.Count > 0 ? _nudgePool.Pop() : new NudgeEntry();
            entry.View = view;
            entry.SlotPosition = returnPosition;
            entry.Offset = offset;
            entry.Duration = Mathf.Max(duration, 0.0001f);
            entry.Elapsed = 0f;
            entry.OnComplete = onComplete;
            entry.StartPosition = startPosition;
            entry.MotionChecked = false;
            _nudges.Add(entry);
        }

        /// <summary>Removes a view's running nudge without completing it. Returns the old callback so callers can inherit it.</summary>
        internal Action CancelNudge(IBalloonMotionView view)
        {
            var (callback, _) = CancelNudgeInternal(view);
            return callback;
        }

        private (Action callback, Vector3? returnPosition) CancelNudgeInternal(IBalloonMotionView view)
        {
            for (var i = 0; i < _nudges.Count; i++)
            {
                if (!ReferenceEquals(_nudges[i].View, view))
                {
                    continue;
                }

                var oldComplete = _nudges[i].OnComplete;
                var oldSlotPos = _nudges[i].SlotPosition;
                RecycleNudge(i);
                return (oldComplete, oldSlotPos);
            }

            return (null, null);
        }

        /// <summary>Replaces the completion callback of a running nudge so subsequent work can chain after it.</summary>
        internal void ReplaceOnComplete(IBalloonMotionView view, Action onComplete)
        {
            for (var i = 0; i < _nudges.Count; i++)
            {
                if (ReferenceEquals(_nudges[i].View, view))
                {
                    _nudges[i].OnComplete = onComplete;
                    return;
                }
            }

            // No active nudge — invoke immediately.
            onComplete?.Invoke();
        }

        private void TickNudges()
        {
            var deltaTime = Time.deltaTime;

            // Backwards so swap-remove doesn't skip elements.
            for (var i = _nudges.Count - 1; i >= 0; i--)
            {
                var entry = _nudges[i];
                entry.Elapsed += deltaTime;
                var progress = Mathf.Clamp01(entry.Elapsed / entry.Duration);

                var reach = progress < 0.5f
                    ? EaseOutQuad(progress * 2f)
                    : 1f - EaseOutQuad((progress - 0.5f) * 2f);
                // Blend from the initial visual position toward the correct return position so the
                // balloon doesn't snap on frame one but still ends up at the right spot.
                var basePos = Vector3.Lerp(entry.StartPosition, entry.SlotPosition, progress);
                var targetPos = basePos + entry.Offset * reach;
                entry.View.ApplyNudgePosition(targetPos);

                // Mid-nudge motion verification: at ~25% progress check whether the view actually moved.
                if (!entry.MotionChecked && progress >= 0.25f)
                {
                    entry.MotionChecked = true;
                    var delta = (targetPos - entry.StartPosition).sqrMagnitude;
                    if (delta < 0.0001f)
                    {
                        Debug.LogWarning(
                            $"[Nudge] MotionTicker: NO MOTION at {progress:P0} — " +
                            $"offset={entry.Offset.magnitude:F4} reach={reach:F4} " +
                            $"startPos={entry.StartPosition} curPos={targetPos}");
                    }
                    else
                    {
                        Debug.Log(
                            $"[Nudge] MotionTicker: motion OK at {progress:P0} — " +
                            $"delta={Mathf.Sqrt(delta):F4} offset={entry.Offset.magnitude:F4}");
                    }
                }

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
            public Vector3 StartPosition;
            public bool MotionChecked;
        }
    }
}
