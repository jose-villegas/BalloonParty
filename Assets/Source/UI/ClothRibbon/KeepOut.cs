using UnityEngine;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>
    ///     A round area to stay out of, stretched upward into a capsule: every point within <see cref="Radius" /> of
    ///     the segment from <see cref="Bottom" /> to <see cref="Top" />. A plain circle has both ends the same.
    /// </summary>
    internal readonly struct KeepOut
    {
        internal readonly Vector2 Bottom;
        internal readonly Vector2 Top;
        internal readonly float Radius;

        internal KeepOut(Vector2 bottom, Vector2 top, float radius)
        {
            Bottom = bottom;
            Top = top;
            Radius = radius;
        }

        internal float DistanceFrom(Vector2 point)
        {
            return Vector2.Distance(point, Nearest(point));
        }

        internal bool Contains(Vector2 point)
        {
            return DistanceFrom(point) < Radius;
        }

        /// <summary>
        ///     <paramref name="point" /> moved straight out to the edge if it is inside, else unchanged, with the
        ///     outward direction there.
        /// </summary>
        internal Vector2 PushOut(Vector2 point, out Vector2 outward)
        {
            var nearest = Nearest(point);
            var away = point - nearest;
            var distance = away.magnitude;
            outward = distance > 1e-5f ? away / distance : Vector2.up;
            return distance >= Radius ? point : nearest + outward * Radius;
        }

        private Vector2 Nearest(Vector2 point)
        {
            var spine = Top - Bottom;
            var length = spine.sqrMagnitude;
            var along = length > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - Bottom, spine) / length) : 0f;
            return Bottom + spine * along;
        }
    }
}
