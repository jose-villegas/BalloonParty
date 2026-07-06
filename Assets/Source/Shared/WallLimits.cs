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
