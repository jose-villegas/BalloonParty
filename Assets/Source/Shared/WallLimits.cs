using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The four play-area walls unpacked from the clockwise <see cref="Vector4" /> convention
    ///     (x = top, y = right, z = bottom, w = left) used by
    ///     <see cref="IGameConfiguration.LimitsClockwise" />. Owning the wall layout and the
    ///     box-bounce in one place keeps the live projectile flight and the aim-prediction trace
    ///     from drifting apart when the convention changes. A readonly struct — no allocation.
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
        ///     normal of every wall it crossed (<see cref="Vector3.zero" /> if it stayed inside).
        ///     Reflect a velocity with <c>Vector2.Reflect(dir, reflectNormal.normalized)</c>.
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
