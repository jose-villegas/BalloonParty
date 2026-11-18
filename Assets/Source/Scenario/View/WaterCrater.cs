using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Scenario.View
{
    /// <summary>
    ///     The crater a blast (a bomb, the nuke) leaves in the water, and the shape it is dug in. A blast moves
    ///     water instead of pressing on it: over its seconds it carries the water under it outward along the slope
    ///     of a potential (<see cref="Potential" />), so what leaves the middle lands in a ring round it and no
    ///     water appears or is lost. The potential is chosen for the crater it digs (<see cref="Profile" />): flat
    ///     over its inner half, a wall, and past the edge a rim a third as tall as the crater is deep. Meanwhile
    ///     the water answers as it always does: the rim runs off as a wave and the crater floods back in, which
    ///     the dig holds off while it lasts (<see cref="DugBy" />).
    ///     A blast with a <see cref="Line" /> (a stretch of a piercing shot's flight) digs a furrow instead: the
    ///     same potential runs the line from end to end at a steady pace, digging as it goes. One stretch after
    ///     another makes one unbroken furrow, as deep down its middle however the path is cut into stretches, which
    ///     opens in the order the shot flew it and closes behind in the same order, its water thrown out to both
    ///     sides.
    ///     <c>WaterRipple.shader</c>'s step evaluates the same potential; <see cref="WaterRippleBuffer" /> feeds it.
    /// </summary>
    internal readonly struct WaterCrater
    {
        // The crater's edge (where the dig turns into the rim) in spreads of the potential: the root of
        // 1 + u + u²/2 = u³/2, u being the distance in spreads squared.
        internal const float EdgeInSpreads = 1.5065f;

        // The farthest a furrow's dig moves in one step of the simulation, in spreads: past it the furrow would
        // be a row of holes, one a step, and short of it still shows the steps as rungs.
        internal const float MaxSpreadsAStep = 0.8f;

        // What a furrow's dig leaves down its middle as it passes, per unit of its scale, times the length it
        // runs and its spread (both in texels): the potential's Laplacian summed along a line through its middle,
        // 5/4 of the square root of pi.
        private const float FurrowPerScale = 2.2156f;

        // World units from the middle to the crater's edge.
        internal readonly float Radius;
        // Rest depths the dig takes out of the middle in all, or for a furrow out of the middle of its width,
        // all along it. The water flooding back while it lasts takes some of that away, so a long dig leaves a
        // shallower crater than a short one.
        internal readonly float Depth;
        // Seconds the dig lasts: the crater opens in the first of them and is held open for the rest. For a
        // furrow, the seconds the dig takes to run its line.
        internal readonly float Seconds;
        // The line a furrow is dug along, from its first end to its last, in world units, with the blast's
        // position at its middle. Zero for a crater.
        internal readonly Vector2 Line;

        // The potential's spread, in world units: its scale, as a Gaussian's radius is.
        internal float Spread => Radius / EdgeInSpreads;

        // Whether the dig runs along a line (a furrow) or stays where it is (a crater).
        internal bool Runs => Line.sqrMagnitude > 1e-8f;

        internal WaterCrater(float radius, float depth, float seconds, Vector2 line = default)
        {
            Radius = radius;
            Depth = depth;
            Seconds = seconds;
            Line = line;
        }

        /// <summary>
        ///     The crater a reported blast digs: as wide as the blast times <paramref name="radiusScale" />, as deep
        ///     as its strength times <paramref name="depthPerStrength" /> rest depths, dug over the blast's seconds
        ///     (never less than <paramref name="stepSeconds" />, one step of the simulation), along its line if it
        ///     has one. A furrow's dig is also given the time to run its line no faster than
        ///     <see cref="MaxSpreadsAStep" />: a lance outruns the simulation, and its narrow furrow follows a few
        ///     hundredths of a second behind it rather than break into holes.
        /// </summary>
        internal static WaterCrater From(
            ImpactEventBus.Blast blast, float radiusScale, float depthPerStrength, float stepSeconds)
        {
            var radius = Mathf.Max(0f, blast.Radius * radiusScale);
            var seconds = Mathf.Max(blast.Seconds, stepSeconds);
            if (radius > 0f)
            {
                var stepsToRun = blast.Line.magnitude / (radius / EdgeInSpreads * MaxSpreadsAStep);
                seconds = Mathf.Max(seconds, stepsToRun * stepSeconds);
            }

            return new WaterCrater(radius, Mathf.Max(0f, blast.Strength * depthPerStrength), seconds, blast.Line);
        }

        /// <summary>
        ///     Joins two furrows into one when <paramref name="next" /> carries straight on from where
        ///     <paramref name="first" /> ends, as wide and as deep: the stretches a shot flew in one frame's fixed
        ///     steps. One furrow of the whole length, run in the time of both. A frame that runs many steps at once
        ///     (a hitch) then asks for one dig, not one a step, however far the shot flew in it. False, and nothing
        ///     joined, for craters, a turn (a bounce) or a gap.
        /// </summary>
        internal static bool TryJoin(
            Vector2 firstOrigin, in WaterCrater first, Vector2 nextOrigin, in WaterCrater next,
            out Vector2 origin, out WaterCrater joined)
        {
            origin = firstOrigin;
            joined = first;
            if (!first.Runs || !next.Runs || Mathf.Abs(first.Radius - next.Radius) > 1e-4f
                || Mathf.Abs(first.Depth - next.Depth) > 1e-4f)
            {
                return false;
            }

            var start = firstOrigin - first.Line * 0.5f;
            var joint = firstOrigin + first.Line * 0.5f;
            var end = nextOrigin + next.Line * 0.5f;
            if ((nextOrigin - next.Line * 0.5f - joint).sqrMagnitude > 1e-4f
                || Vector2.Dot(first.Line.normalized, next.Line.normalized) < 0.999f)
            {
                return false;
            }

            origin = (start + end) * 0.5f;
            joined = new WaterCrater(first.Radius, first.Depth, first.Seconds + next.Seconds, end - start);
            return true;
        }

        /// <summary>
        ///     The share of the dig done <paramref name="age" /> seconds in, 0 to 1. A crater's is fastest at the
        ///     start, as a blast is, and eases off to nothing: the crater opens at once, and the rest of the dig
        ///     holds it open against the water flooding back for as long as it lasts. A furrow's is steady, as its
        ///     share is also how far along its line the dig has come (<see cref="OffsetAt" />).
        /// </summary>
        internal float DugBy(float age)
        {
            if (Seconds <= 0f)
            {
                return 1f;
            }

            var done = Mathf.Clamp01(age / Seconds);
            if (Runs)
            {
                return done;
            }

            var left = 1f - done;
            return 1f - left * left;
        }

        /// <summary>
        ///     Where the dig is, from the blast's position, once <paramref name="dug" /> of it (0 to 1) is done: a
        ///     furrow's runs its line from the first end to the last, a crater's stays put.
        /// </summary>
        internal Vector2 OffsetAt(float dug)
        {
            return Runs ? Line * (dug - 0.5f) : Vector2.zero;
        }

        /// <summary>
        ///     What the potential is scaled by over the whole dig, on a grid of <paramref name="texelsPerUnit" />.
        ///     A crater's, so its middle ends <see cref="Depth" /> down: the potential's Laplacian at the middle is
        ///     4/3 over its spread squared. A furrow's, so the middle of its width ends that far down all along it:
        ///     in proportion to its length, so a path digs the same however it is cut into stretches, and a shot
        ///     that stalls digs nothing.
        /// </summary>
        internal float PotentialScale(float texelsPerUnit)
        {
            var spreadTexels = Spread * texelsPerUnit;
            return Runs
                ? Depth * Line.magnitude * texelsPerUnit * spreadTexels / FurrowPerScale
                : Depth * 0.75f * spreadTexels * spreadTexels;
        }

        /// <summary>
        ///     The blast's potential at <paramref name="u" />, the distance from its middle in spreads, squared:
        ///     1 at the middle, gone by a few spreads. Water moves down its slope.
        /// </summary>
        internal static float Potential(float u)
        {
            return (1f + u * (2f / 3f + u / 6f)) * Mathf.Exp(-u);
        }

        /// <summary>
        ///     The height the dig leaves at <paramref name="u" /> (as in <see cref="Potential" />) per unit of depth:
        ///     -1 at the middle, 0 at the edge, positive in the rim. It is the potential's Laplacian, so over the
        ///     whole plane it sums to nothing.
        /// </summary>
        internal static float Profile(float u)
        {
            return -(1f + u + 0.5f * u * u - 0.5f * u * u * u) * Mathf.Exp(-u);
        }
    }
}
