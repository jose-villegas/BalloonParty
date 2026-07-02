using System;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     A single-point <see cref="ICinematicFocus" /> reading a live position (the level-up's tipping
    ///     trail). min == max == center, which the rig treats as "hard-clamp after easing" — one point
    ///     never widens the box past the view, and it must never leave the frustum.
    /// </summary>
    internal sealed class PointFocus : ICinematicFocus
    {
        private readonly Func<Vector3> _position;

        public PointFocus(Func<Vector3> position)
        {
            _position = position;
        }

        public bool TryGetFocus(out Vector3 center, out Vector3 min, out Vector3 max)
        {
            center = _position();
            min = center;
            max = center;
            return true;
        }
    }
}
