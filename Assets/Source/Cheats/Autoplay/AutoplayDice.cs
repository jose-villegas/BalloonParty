#if UNITY_ENABLE_CHECKS
using UnityEngine;

namespace BalloonParty.Cheats.Autoplay
{
    /// <summary>The human player's randomness, pure so it's EditMode-testable. Uniforms are in [0, 1).</summary>
    internal static class AutoplayDice
    {
        /// <summary>
        ///     Seconds from a ready cannon to the shot: a floor plus an exponential wait of the given mean, so
        ///     shots arrive as a Poisson process.
        /// </summary>
        internal static float ReactionDelay(float floor, float mean, float uniform)
        {
            return floor - mean * Mathf.Log(1f - uniform);
        }

        /// <summary>A standard normal sample from two uniforms (Box–Muller).</summary>
        internal static float Normal(float uniform1, float uniform2)
        {
            return Mathf.Sqrt(-2f * Mathf.Log(1f - uniform1)) * Mathf.Cos(2f * Mathf.PI * uniform2);
        }
    }
}
#endif
