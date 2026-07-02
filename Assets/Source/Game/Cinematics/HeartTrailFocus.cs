using System.Collections.Generic;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     An <see cref="ICinematicFocus" /> over the in-flight heart trails: centroid + bounding box of
    ///     <see cref="HeartTrailTracker.Active" />, refreshed each query into a reused buffer. Empty set
    ///     → no focus (the rig holds position while the next heart launches).
    /// </summary>
    internal sealed class HeartTrailFocus : ICinematicFocus
    {
        private readonly HeartTrailTracker _tracker;
        private readonly List<Vector3> _positions = new();

        public HeartTrailFocus(HeartTrailTracker tracker)
        {
            _tracker = tracker;
        }

        public bool TryGetFocus(out Vector3 center, out Vector3 min, out Vector3 max)
        {
            _positions.Clear();
            var active = _tracker.Active;
            for (var i = 0; i < active.Count; i++)
            {
                if (active[i] != null)
                {
                    _positions.Add(active[i].position);
                }
            }

            if (_positions.Count == 0)
            {
                center = min = max = default;
                return false;
            }

            center = _positions.Centroid(_positions.Count);
            var bounds = _positions.Bounds(_positions.Count);
            min = bounds.min;
            max = bounds.max;
            return true;
        }
    }
}
