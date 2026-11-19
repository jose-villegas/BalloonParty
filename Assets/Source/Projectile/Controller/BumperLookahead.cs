using System.Collections.Generic;
using BalloonParty.Projectile.Model;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Projectile.Controller
{
    /// <summary>
    ///     Whether a mushroom stands in the way of a ball on a straight run ahead of it: what the doomed glide's
    ///     path check, the cruise lookahead and the pierce telegraph ask, so the three agree with the flight
    ///     (<see cref="ProjectileMotionResolver" />) on what the ball would bounce off.
    /// </summary>
    /// <remarks>
    ///     Nothing stands in the way of a lance: a piercing ball flies through the mushrooms and cuts their caps,
    ///     on every leg it is looked along (<see cref="TryFindAhead" /> is told whether the ball is piercing).
    ///     For a ball that is not, two things about its own flight take a mushroom out of its way, and both hold
    ///     only until its next wall, so only for the leg the ball is on:
    ///     <list type="bullet">
    ///         <item>
    ///             A spent stretch (<see cref="MushroomChainRule.IsSpent" />): the mushrooms let the ball through
    ///             until it meets a wall. The wall clears the chain, so on every leg past it they bounce the ball
    ///             again.
    ///         </item>
    ///         <item>
    ///             The mushroom the ball is leaving (<see cref="ProjectileFlightState.LeavingBumperId" />), which
    ///             a line close to its tangent would be read as entering.
    ///         </item>
    ///     </list>
    /// </remarks>
    internal static class BumperLookahead
    {
        /// <summary>
        ///     The first mushroom the ball would bounce off on <paramref name="run" />, and how far along it: zero
        ///     for a run that starts inside one. False when the run is clear of them, and for a ball that
        ///     <paramref name="isPiercing" />, which bounces off none.
        /// </summary>
        internal static bool TryFindAhead(
            IReadOnlyList<BumperCircle> bumpers, ProjectileFlightState flight, in MushroomChainRules rules,
            in Run run, bool isPiercing, out float distance)
        {
            distance = 0f;
            if (isPiercing || bumpers.Count == 0)
            {
                return false;
            }

            var isTheBallsOwnLeg = run.Leg == 0;
            if (isTheBallsOwnLeg && MushroomChainRule.IsSpent(flight.MushroomChain, rules))
            {
                return false;
            }

            var heading = run.Direction.normalized;
            var leaving = isTheBallsOwnLeg ? flight.LeavingBumperId(heading) : MushroomChainState.NoBumper;
            if (!BumperSweep.TryFindFirst(bumpers, run.From, heading, run.Length, leaving, out _, out var entry))
            {
                return false;
            }

            distance = Mathf.Max(0f, entry);
            return true;
        }

        /// <summary>One straight run of a path looked along.</summary>
        internal readonly struct Run
        {
            public readonly Vector2 From;

            /// <summary>The way the run goes. Any length.</summary>
            public readonly Vector2 Direction;

            public readonly float Length;

            /// <summary>Which leg of the path this is: 0 runs from the ball itself to its first wall, and each
            /// wall the path reflects off starts the next.</summary>
            public readonly int Leg;

            public Run(Vector2 from, Vector2 direction, float length, int leg)
            {
                From = from;
                Direction = direction;
                Length = length;
                Leg = leg;
            }
        }
    }
}
