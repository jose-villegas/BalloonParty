using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The swept test of a ball against the board's bumpers: which bumper circle the ball's centre enters
    ///     first on a straight run, and how far along. The flight, and everything that looks ahead of the ball,
    ///     ask the one test, so they agree on what a mushroom is in the way of.
    /// </summary>
    /// <remarks>
    ///     A run is tested whole, from its start to its end, so no speed carries a ball through a bumper between
    ///     two fixed steps. The answer is the exact entry point of the centre's path into the circle
    ///     (<see cref="CircleContact.TryFindEntry" />), not where a step happened to end, which is what
    ///     <see cref="IBumperBounceRule" /> asks of its callers.
    /// </remarks>
    internal static class BumperSweep
    {
        // A ball this near the far edge of a circle, measured along its path, is leaving it: within a rounding
        // error of the edge, not inside.
        private const float LeavingEpsilon = 1e-4f;

        /// <summary>
        ///     The first bumper the ball's centre enters flying <paramref name="length" /> from
        ///     <paramref name="from" /> along <paramref name="direction" /> (unit length), or false when the run
        ///     is clear. <paramref name="entryDistance" /> is how far along the run the entry is. It is zero or
        ///     less when the run starts inside the circle (see <see cref="TryFindEntry" />): the entry then lies
        ///     that far behind the start, and it wins over every entry ahead.
        ///     <paramref name="skipId" /> names a bumper to leave out: the one the ball is leaving, which a line
        ///     close to its tangent would be read as entering again. Pass
        ///     <see cref="MushroomChainState.NoBumper" /> for none.
        /// </summary>
        internal static bool TryFindFirst(
            IReadOnlyList<BumperCircle> bumpers, Vector2 from, Vector2 direction, float length, int skipId,
            out int index, out float entryDistance)
        {
            index = -1;
            entryDistance = float.MaxValue;
            for (var i = 0; i < bumpers.Count; i++)
            {
                var bumper = bumpers[i];
                if (bumper.Id == skipId || !TryFindEntry(from, direction, bumper, out var distance)
                    || distance > length || distance >= entryDistance)
                {
                    continue;
                }

                index = i;
                entryDistance = distance;
            }

            return index >= 0;
        }

        /// <summary>
        ///     How far along <paramref name="direction" /> (unit length) from <paramref name="from" /> the ball's
        ///     centre enters <paramref name="bumper" />. Ahead of the ball the distance is above zero. A ball
        ///     already inside the circle (set down there by the tail of a wall bounce or of a deflect) entered
        ///     it behind itself: the distance is then zero or less, the entry being where its line came in.
        ///     False when the line misses, when the circle is behind the ball, or when the ball is on its way
        ///     out within a rounding error of the edge.
        /// </summary>
        internal static bool TryFindEntry(Vector2 from, Vector2 direction, in BumperCircle bumper, out float distance)
        {
            if (CircleContact.TryFindEntry(from, direction, bumper.Center, bumper.Radius, out _, out distance))
            {
                return true;
            }

            distance = 0f;
            var toFrom = from - bumper.Center;
            var inside = (bumper.Radius * bumper.Radius) - toFrom.sqrMagnitude;
            if (bumper.Radius <= 0f || inside <= 0f || direction.sqrMagnitude < 1e-8f)
            {
                return false;
            }

            // Inside the circle the line has an entry behind the ball and an exit ahead of it.
            var along = Vector2.Dot(toFrom, direction);
            var halfChord = Mathf.Sqrt((along * along) + inside);
            if (halfChord - along <= LeavingEpsilon)
            {
                return false;
            }

            distance = -along - halfChord;
            return true;
        }
    }
}
