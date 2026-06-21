using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class VectorExtensions
    {
        internal static Vector3 WithY(this Vector3 v, float y)
        {
            return new Vector3(v.x, y, v.z);
        }

        internal static Vector2 WithY(this Vector2 v, float y)
        {
            return new Vector2(v.x, y);
        }

        /// <summary>The 90° counter-clockwise perpendicular in the XY plane (z zeroed).</summary>
        internal static Vector3 PerpendicularXY(this Vector3 v)
        {
            return new Vector3(-v.y, v.x, 0f);
        }
    }
}
