using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Identity-based flight tracking for trail orbs. Manages in-flight state,
    ///     forward/retroactive interception, and selective pause/resume. Composed by
    ///     trail services that need cinematic integration — the service handles its
    ///     own spawning and calls Register/Unregister to keep the tracker in sync.
    /// </summary>
    internal class TrailTracker<TId> where TId : struct, IEquatable<TId>
    {
        private readonly Dictionary<TId, Transform> _inFlightTrails = new();
        private readonly HashSet<TId> _pausedTrails = new();
        private readonly Dictionary<TId, Action<Transform>> _trackedTrails = new();

        internal void ClearTrackedTrail(TId id)
        {
            _trackedTrails.Remove(id);
        }

        internal Transform GetTrailTransform(TId id)
        {
            return _inFlightTrails.TryGetValue(id, out var t) ? t : null;
        }

        /// <summary>
        ///     Returns true if forward-tracking was registered for this ID before
        ///     spawn. The caller should spawn with unscaled time when true.
        /// </summary>
        internal bool IsTracked(TId id, out Action<Transform> callback)
        {
            return _trackedTrails.TryGetValue(id, out callback);
        }

        internal void PauseWhere(Func<TId, bool> predicate)
        {
            foreach (var kvp in _inFlightTrails)
            {
                if (!predicate(kvp.Key))
                {
                    continue;
                }

                if (kvp.Value != null)
                {
                    kvp.Value.DOPause();
                    _pausedTrails.Add(kvp.Key);
                }
            }
        }

        internal void Register(TId id, Transform transform)
        {
            _inFlightTrails[id] = transform;
        }

        internal void ResumeAll()
        {
            foreach (var id in _pausedTrails)
            {
                if (_inFlightTrails.TryGetValue(id, out var trail) && trail != null)
                {
                    trail.DOPlay();
                }
            }

            _pausedTrails.Clear();
        }

        internal void ResumeTrail(TId id)
        {
            var t = GetTrailTransform(id);
            if (t != null)
            {
                t.DOPlay();
            }
        }

        /// <summary>
        ///     Registers interest in a trail. If already in-flight (retroactive),
        ///     pauses it, switches tweens to unscaled time, and fires the callback
        ///     immediately. Otherwise stores the callback for when the trail spawns.
        /// </summary>
        internal void TrackTrail(TId id, Action<Transform> onSpawned)
        {
            if (_inFlightTrails.TryGetValue(id, out var existingTrail))
            {
                existingTrail.DOPause();

                var tweens = DOTween.TweensByTarget(existingTrail);
                if (tweens != null)
                {
                    foreach (var tween in tweens)
                    {
                        tween.SetUpdate(true);
                    }
                }

                onSpawned?.Invoke(existingTrail);
                return;
            }

            _trackedTrails[id] = onSpawned;
        }

        internal void Unregister(TId id)
        {
            _inFlightTrails.Remove(id);
            _pausedTrails.Remove(id);
        }
    }
}
