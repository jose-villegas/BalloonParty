using System.Collections.Generic;
using BalloonParty.Game.Health;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     An <see cref="ICinematicFocus" /> over the in-flight heart trails: the focus centre is the
    ///     <em>oldest</em> heart — the one about to land and pop, which is the beat the camera must not
    ///     lose — while the bounding box still spans every trail so the rig keeps the rest in frame when
    ///     it can. A plain centroid drifts back up toward the UI with every new launch (and once the
    ///     UI-to-pile box outgrows the view, the frustum clamp centres on it), pushing the pops off
    ///     frame. Empty set → no focus (the rig holds position while the next heart launches).
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

            // The tracker preserves launch order, so [0] is the heart closest to landing.
            center = _positions[0];
            var bounds = _positions.Bounds(_positions.Count);
            min = bounds.min;
            max = bounds.max;
            return true;
        }
    }
}
