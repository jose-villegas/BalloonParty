using UnityEngine;

namespace BalloonParty.Item.Snipe
{
    /// <summary>The pencil line's timing, read off the line prefab.</summary>
    internal readonly struct SnipeLanceStrokeTiming
    {
        internal readonly float TipSpeed;
        internal readonly float HoldSeconds;
        internal readonly float FadeSeconds;

        /// <param name="tipSpeed">World units a second the pencil tip runs; 0 or less draws the run at once.</param>
        internal SnipeLanceStrokeTiming(float tipSpeed, float holdSeconds, float fadeSeconds)
        {
            TipSpeed = Mathf.Max(0f, tipSpeed);
            HoldSeconds = Mathf.Max(0f, holdSeconds);
            FadeSeconds = Mathf.Max(0f, fadeSeconds);
        }
    }

    /// <summary>
    ///     One pencil stroke's progress: the tip runs at a steady speed toward the end of the run, never past
    ///     it. The end can still be moving (the shot that is drawing it is still flying), so the stroke only
    ///     holds and fades once the run is closed and the tip has reached its end.
    /// </summary>
    internal struct SnipeLanceStroke
    {
        private float _drawn;
        private float _sinceReached;
        private bool _reached;

        /// <summary>How far along the run the tip has drawn, in world units.</summary>
        internal float Drawn => _drawn;

        /// <param name="length">The run's length so far: up to the shot while it flies, then up to its wall.</param>
        /// <param name="closed">Whether the run has ended, so <paramref name="length" /> won't grow again.</param>
        /// <param name="alpha">The whole stroke's opacity.</param>
        /// <returns>False once the stroke has fully faded and its line can go back to the pool.</returns>
        internal bool Advance(float deltaTime, float length, bool closed, in SnipeLanceStrokeTiming timing,
            out float alpha)
        {
            length = Mathf.Max(0f, length);
            _drawn = timing.TipSpeed > 0f ? Mathf.Min(_drawn + (timing.TipSpeed * deltaTime), length) : length;

            if (_reached)
            {
                _sinceReached += deltaTime;
            }
            else if (closed && _drawn >= length)
            {
                _reached = true;
            }

            var fadeT = !_reached || _sinceReached < timing.HoldSeconds
                ? 0f
                : timing.FadeSeconds > 0f
                    ? Mathf.Clamp01((_sinceReached - timing.HoldSeconds) / timing.FadeSeconds)
                    : 1f;
            alpha = 1f - fadeT;

            return alpha > 0f;
        }
    }
}
