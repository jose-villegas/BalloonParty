using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    /// General-purpose math constants and pure functions not covered by
    /// <see cref="Mathf"/>.
    /// </summary>
    internal static class MathUtils
    {
        internal const float GoldenAngle = 2.39996323f;
        internal const float TwoPi = 6.283185f;

        /// <summary>
        /// Fractional part of <paramref name="x"/>, always in [0, 1).
        /// </summary>
        internal static float Frac(float x)
        {
            return x - Mathf.Floor(x);
        }

        internal static float SqrDistance2D(Vector2 a, Vector2 b)
        {
            var dx = a.x - b.x;
            var dy = a.y - b.y;
            return dx * dx + dy * dy;
        }

        internal static bool WithinRadius(Vector2 a, Vector2 b, float radius)
        {
            return SqrDistance2D(a, b) <= radius * radius;
        }
    }
}
