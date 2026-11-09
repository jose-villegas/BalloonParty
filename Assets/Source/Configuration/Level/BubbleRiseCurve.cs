using UnityEngine;

namespace BalloonParty.Configuration.Level
{
    /// <summary>
    ///     Pure answer to "what share of a line's places are bubbles, this far into a water level?": none during
    ///     the grace, then a straight climb from <see cref="IBubbleRiseSettings.StartShare" /> to
    ///     <see cref="IBubbleRiseSettings.MaxShare" /> over <see cref="IBubbleRiseSettings.RampSeconds" />, then
    ///     held. The longer the player stays on the level, the more of every line is bubbles.
    /// </summary>
    internal static class BubbleRiseCurve
    {
        /// <param name="secondsOnLevel">Seconds of play on the level so far (the spawn clock's holds apply).</param>
        public static float ShareAt(IBubbleRiseSettings settings, float secondsOnLevel)
        {
            var sinceGrace = secondsOnLevel - settings.GraceSeconds;
            if (sinceGrace < 0f)
            {
                return 0f;
            }

            var ramp = settings.RampSeconds > 0f ? Mathf.Clamp01(sinceGrace / settings.RampSeconds) : 1f;
            return Mathf.Lerp(settings.StartShare, settings.MaxShare, ramp);
        }
    }
}
