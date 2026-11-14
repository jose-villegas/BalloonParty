using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>The pencil line's timing, read off the line prefab.</summary>
    internal readonly struct SnipeLanceStrokeTiming
    {
        internal readonly float DrawSeconds;
        internal readonly float HoldSeconds;
        internal readonly float FadeSeconds;

        internal float TotalSeconds => DrawSeconds + HoldSeconds + FadeSeconds;

        internal SnipeLanceStrokeTiming(float drawSeconds, float holdSeconds, float fadeSeconds)
        {
            DrawSeconds = Mathf.Max(0f, drawSeconds);
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            FadeSeconds = Mathf.Max(0f, fadeSeconds);
        }
    }

    /// <summary>
    ///     One pencil stroke's shape over time: the tip runs from the run's start to its wall, the line holds,
    ///     then it fades. The drawn fraction eases out so the stroke snaps off the start like a flicked pencil
    ///     and settles onto the wall.
    /// </summary>
    internal static class SnipeLanceStroke
    {
        /// <returns>False once the stroke has fully faded and its line can go back to the pool.</returns>
        internal static bool Evaluate(float elapsed, in SnipeLanceStrokeTiming timing, out float drawn, out float alpha)
        {
            var drawT = timing.DrawSeconds > 0f ? Mathf.Clamp01(elapsed / timing.DrawSeconds) : 1f;
            drawn = 1f - ((1f - drawT) * (1f - drawT));

            var fadeStart = timing.DrawSeconds + timing.HoldSeconds;
            var fadeT = timing.FadeSeconds > 0f
                ? Mathf.Clamp01((elapsed - fadeStart) / timing.FadeSeconds)
                : (elapsed >= fadeStart ? 1f : 0f);
            alpha = 1f - fadeT;

            return elapsed < timing.TotalSeconds;
        }
    }
}
