using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The four play-area walls unpacked from <see cref="IProjectileFlightConfig.LimitsClockwise" />
    ///     (x = top, y = right, z = bottom, w = left).
    /// </summary>
    internal readonly struct WallLimits
    {
        public readonly float Top;
        public readonly float Right;
        public readonly float Bottom;
        public readonly float Left;

        public WallLimits(Vector4 clockwise)
        {
            Top = clockwise.x;
            Right = clockwise.y;
            Bottom = clockwise.z;
            Left = clockwise.w;
        }

        /// <summary>
        ///     Finds the first wall the ray from <paramref name="position" /> along
        ///     <paramref name="direction" /> crosses: the crossing point and the summed inward normal
        ///     (a corner hit sums both walls, matching <see cref="Reflect" />'s convention). False when
        ///     the direction is (near-)zero.
        /// </summary>
        public bool TryFindCrossing(Vector3 position, Vector3 direction, out Vector3 crossing, out Vector3 normal)
        {
            const float parallelEpsilon = 1e-6f;
            const float cornerEpsilon = 1e-5f;

            crossing = position;
            normal = Vector3.zero;

            var bestT = float.PositiveInfinity;
            if (direction.x > parallelEpsilon)
            {
                bestT = (Right - position.x) / direction.x;
            }
            else if (direction.x < -parallelEpsilon)
            {
                bestT = (Left - position.x) / direction.x;
            }

            var verticalT = float.PositiveInfinity;
            if (direction.y > parallelEpsilon)
            {
                verticalT = (Top - position.y) / direction.y;
            }
            else if (direction.y < -parallelEpsilon)
            {
                verticalT = (Bottom - position.y) / direction.y;
            }

            var t = Mathf.Min(bestT, verticalT);
            if (float.IsInfinity(t) || t < 0f)
            {
                return false;
            }

            crossing = position + direction * t;
            if (bestT <= t + cornerEpsilon)
            {
                normal += direction.x > 0f ? Vector3.left : Vector3.right;
            }

            if (verticalT <= t + cornerEpsilon)
            {
                normal += direction.y > 0f ? Vector3.down : Vector3.up;
            }

            return true;
        }

        /// <summary>Clamps a position inside the walls WITHOUT reflecting or reporting a crossing —
        /// used to keep a snapped point (e.g. a deflect contact off a near-wall balloon, whose collider
        /// radius can extend past the wall) in-bounds so the next step doesn't read it as a spurious
        /// wall bounce.</summary>
        public Vector3 ClampInside(Vector3 position)
        {
            position.x = Mathf.Clamp(position.x, Left, Right);
            position.y = Mathf.Clamp(position.y, Bottom, Top);
            return position;
        }

        /// <summary>
        ///     Mirror-reflects <paramref name="position" /> back across every wall it crossed (the
        ///     exact billiard continuation — the overshoot travels on along the reflected heading, so
        ///     neither distance nor time is lost), reporting the summed inward normal
        ///     (<see cref="Vector3.zero" /> if none) and the wall-projected contact point for
        ///     presentation. Clamping instead would keep the full parallel advance while zeroing the
        ///     perpendicular overshoot, displacing every post-bounce path laterally by up to one step
        ///     — an error the deterministic-shot work cannot absorb.
        /// </summary>
        public Vector3 Reflect(Vector3 position, out Vector3 reflectNormal, out Vector3 wallContact)
        {
            reflectNormal = Vector3.zero;
            wallContact = position;

            if (position.y > Top)
            {
                reflectNormal += Vector3.down;
                wallContact.y = Top;
                position.y = 2f * Top - position.y;
            }

            if (position.x > Right)
            {
                reflectNormal += Vector3.left;
                wallContact.x = Right;
                position.x = 2f * Right - position.x;
            }

            if (position.y < Bottom)
            {
                reflectNormal += Vector3.up;
                wallContact.y = Bottom;
                position.y = 2f * Bottom - position.y;
            }

            if (position.x < Left)
            {
                reflectNormal += Vector3.right;
                wallContact.x = Left;
                position.x = 2f * Left - position.x;
            }

            return position;
        }
    }
}
