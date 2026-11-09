using BalloonParty.Configuration.Level;
using UnityEngine;

namespace BalloonParty.Scenario
{
    /// <summary>One frame of the mist reveal: the hole's radius and how thick the mist is outside it.</summary>
    internal readonly struct GroundRevealFrame
    {
        public readonly float Radius;
        public readonly float MistDensity;

        public GroundRevealFrame(float radius, float mistDensity)
        {
            Radius = radius;
            MistDensity = mistDensity;
        }
    }

    /// <summary>
    ///     How large each layer looks at one frame of a reveal, scaled about the camera. The ground sits deeper than
    ///     the sky, so going down it grows into place while the sky grows past the camera, and going up the ground
    ///     shrinks away while the sky settles. <see cref="Incoming" /> and <see cref="Outgoing" /> are the same two
    ///     scales sorted by which level the board's content belongs to.
    /// </summary>
    internal readonly struct GroundRevealDepth
    {
        public readonly float Ground;
        public readonly float Sky;
        public readonly float Incoming;
        public readonly float Outgoing;

        public GroundRevealDepth(float ground, float sky, float incoming, float outgoing)
        {
            Ground = ground;
            Sky = sky;
            Incoming = incoming;
            Outgoing = outgoing;
        }
    }

    /// <summary>
    ///     The timing of the mist reveal, as one curve over how open the ground is. Sky to ground plays it forward:
    ///     the mist rolls in over the sky, then a hole opens from the middle and grows until the meadow fills the
    ///     screen. Ground to sky plays the same curve backwards: the meadow closes into the mist, which then clears
    ///     to the sky.
    /// </summary>
    internal static class GroundRevealCurve
    {
        /// <summary>How open the ground is at this point of a reveal toward <paramref name="target" />.</summary>
        public static float Opening(ScenarioKind target, float progress)
        {
            var clamped = Mathf.Clamp01(progress);
            return target == ScenarioKind.Ground ? clamped : 1f - clamped;
        }

        /// <param name="opening">0 = sky with no mist, 1 = all ground.</param>
        /// <param name="mistInShare">Share of the curve the mist takes to roll in before the hole opens.</param>
        /// <param name="closedRadius">A radius that shows no ground at all (below zero, past the edge's wander).</param>
        /// <param name="openRadius">A radius that puts the hole's edge and its mist past every corner of the view.</param>
        public static GroundRevealFrame Evaluate(
            float opening, float mistInShare, float closedRadius, float openRadius)
        {
            var t = Mathf.Clamp01(opening);
            var share = Mathf.Clamp(mistInShare, 0.01f, 0.99f);
            if (t < share)
            {
                return new GroundRevealFrame(closedRadius, Mathf.SmoothStep(0f, 1f, t / share));
            }

            var open = Mathf.SmoothStep(0f, 1f, (t - share) / (1f - share));
            return new GroundRevealFrame(Mathf.Lerp(closedRadius, openRadius, open), 1f);
        }

        /// <summary>
        ///     The layers' scales over the whole reveal, eased so the travel starts and lands gently. The sky is always
        ///     <paramref name="zoom" /> times the ground, and whichever layer the reveal ends on reaches 1, so the
        ///     scales are continuous at both ends and the level that stays on screen never jumps.
        /// </summary>
        /// <param name="opening">0 = sky with no mist, 1 = all ground (see <see cref="Opening" />).</param>
        /// <param name="zoom">How many times larger the sky looks than the ground; 1 or less = no depth.</param>
        public static GroundRevealDepth Depth(ScenarioKind target, float opening, float zoom)
        {
            var z = Mathf.Max(1f, zoom);
            var ground = Mathf.Lerp(1f / z, 1f, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(opening)));
            var sky = ground * z;
            return target == ScenarioKind.Ground
                ? new GroundRevealDepth(ground, sky, ground, sky)
                : new GroundRevealDepth(ground, sky, sky, ground);
        }
    }
}
