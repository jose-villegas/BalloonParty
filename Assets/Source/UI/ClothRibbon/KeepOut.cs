using UnityEngine;

namespace BalloonParty.UI.ClothRibbon
{
    /// <summary>
    ///     A round area to stay out of, stretched upward into a capsule: every point within <see cref="Radius" /> of
    ///     the segment from <see cref="Bottom" /> to <see cref="Top" />. A plain circle has both ends the same.
    /// </summary>
    internal readonly struct KeepOut
    {
        // A point pushed out lands this far past the edge: exactly on it, rounding can leave it a hair inside.
        private const float Skin = 1e-3f;

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
            return Vector2.Distance(point, Nearest(point, Bottom, Top));
        }

        internal bool Contains(Vector2 point)
        {
            return DistanceFrom(point) < Radius;
        }

        /// <summary>Whether the straight path from <paramref name="from" /> to <paramref name="to" /> passes through.</summary>
        internal bool Blocks(Vector2 from, Vector2 to)
        {
            if (Crosses(from, to, Bottom, Top))
            {
                return true;
            }

            var closest = Mathf.Min(DistanceFrom(from), DistanceFrom(to));
            closest = Mathf.Min(closest, Vector2.Distance(Bottom, Nearest(Bottom, from, to)));
            closest = Mathf.Min(closest, Vector2.Distance(Top, Nearest(Top, from, to)));
            return closest < Radius;
        }

        /// <summary>
        ///     <paramref name="point" /> moved straight out to just past the edge if it is inside, else unchanged, with the
        ///     outward direction there.
        /// </summary>
        internal Vector2 PushOut(Vector2 point, out Vector2 outward)
        {
            var nearest = Nearest(point, Bottom, Top);
            var away = point - nearest;
            var distance = away.magnitude;
            outward = distance > 1e-5f ? away / distance : Vector2.up;
            return distance >= Radius ? point : nearest + outward * (Radius + Skin);
        }

        private static Vector2 Nearest(Vector2 point, Vector2 start, Vector2 end)
        {
            var segment = end - start;
            var length = segment.sqrMagnitude;
            var along = length > 1e-8f ? Mathf.Clamp01(Vector2.Dot(point - start, segment) / length) : 0f;
            return start + segment * along;
        }

        // Strictly crossing segments; touching and overlapping ones are caught by the endpoint distances.
        private static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            return Side(a, b, c) * Side(a, b, d) < 0f && Side(c, d, a) * Side(c, d, b) < 0f;
        }

        private static float Side(Vector2 start, Vector2 end, Vector2 point)
        {
            var segment = end - start;
            var offset = point - start;
            return segment.x * offset.y - segment.y * offset.x;
        }
    }
}
