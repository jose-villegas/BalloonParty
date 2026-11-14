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
        /// <summary>The turn about Z that brings local up onto <paramref name="direction" /> in the XY plane; identity
        /// when it has no length there.</summary>
        internal static Quaternion UpAlongXY(this Vector3 direction)
        {
            if (new Vector2(direction.x, direction.y).sqrMagnitude < 1e-8f)
            {
                return Quaternion.identity;
            }

            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            return Quaternion.Euler(0f, 0f, angle);
        }

        internal static Vector3 PerpendicularXY(this Vector3 v)
        {
            return new Vector3(-v.y, v.x, 0f);
        }
    }
}
