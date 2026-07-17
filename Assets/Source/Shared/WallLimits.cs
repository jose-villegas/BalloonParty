using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The four play-area walls unpacked from <see cref="IGameConfiguration.LimitsClockwise" />
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
        ///     Clamps <paramref name="position" /> inside the walls and reports the summed inward
        ///     normal of every wall crossed (<see cref="Vector3.zero" /> if none).
        /// </summary>
        /// <summary>
        ///     Finds the first wall the ray from <paramref name="position" /> along
        ///     <paramref name="direction" /> crosses: the crossing point and the summed inward normal
        ///     (a corner hit sums both walls, matching <see cref="Clamp" />'s convention). False when
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

        public Vector3 Clamp(Vector3 position, out Vector3 reflectNormal)
        {
            reflectNormal = Vector3.zero;

            if (position.y > Top)
            {
                reflectNormal += Vector3.down;
                position.y = Top;
            }

            if (position.x > Right)
            {
                reflectNormal += Vector3.left;
                position.x = Right;
            }

            if (position.y < Bottom)
            {
                reflectNormal += Vector3.up;
                position.y = Bottom;
            }

            if (position.x < Left)
            {
                reflectNormal += Vector3.right;
                position.x = Left;
            }

            return position;
        }
    }
}
