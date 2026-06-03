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
    }
}
