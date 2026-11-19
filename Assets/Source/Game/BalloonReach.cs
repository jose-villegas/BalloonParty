using UnityEngine;

namespace BalloonParty.Game
{
    /// <summary>
    ///     The room a drifting launch balloon takes up around its body's centre, and from that where the centre may
    ///     be on a screen. Alone it has the whole screen. Two balloons share it one above the other
    ///     (<see cref="RoomAbove" />, <see cref="RoomBelow" />): everything of the upper one, down to the bottom of
    ///     its ribbon, stays a gap above everything of the lower one wherever each is in its room. A ribbon streams
    ///     further sideways than a phone is wide enough to keep two apart, but hangs no lower than at rest, so apart
    ///     in height is the one arrangement in which neither can cover the other's.
    /// </summary>
    internal readonly struct BalloonReach
    {
        /// <summary>The body's radius.</summary>
        internal readonly float Radius;

        /// <summary>From the centre up to the top of the body at the top of its bob.</summary>
        internal readonly float Up;

        /// <summary>From the centre down to the bottom of the ribbon hanging below it, at the bottom of its bob.</summary>
        internal readonly float Down;

        internal BalloonReach(float radius, float up, float down)
        {
            Radius = radius;
            Up = up;
            Down = down;
        }

        /// <summary>Where the centre may be with the screen to itself, <paramref name="clearance" /> from its edges.</summary>
        internal Rect RoomAlone(Rect area, float clearance)
        {
            var side = Radius + clearance;
            return Rect.MinMaxRect(
                area.xMin + side, area.yMin + Down + clearance, area.xMax - side, area.yMax - Up - clearance);
        }

        /// <summary>Where the centre may be as the upper of two balloons, above <paramref name="lower" />.</summary>
        internal Rect RoomAbove(in BalloonReach lower, Rect area, float clearance, float gap)
        {
            var room = RoomAlone(area, clearance);
            room.yMin = room.yMax - Travel(this, lower, area, clearance, gap);
            return room;
        }

        /// <summary>Where the centre may be as the lower of two balloons, below <paramref name="upper" />.</summary>
        internal Rect RoomBelow(in BalloonReach upper, Rect area, float clearance, float gap)
        {
            var room = RoomAlone(area, clearance);
            room.yMax = room.yMin + Travel(upper, this, area, clearance, gap);
            return room;
        }

        // How far each balloon may travel up and down: the height left once both balloons, the gap between them and
        // the room at the edges have theirs, split evenly. None on a screen too short for the two.
        private static float Travel(in BalloonReach upper, in BalloonReach lower, Rect area, float clearance, float gap)
        {
            var taken = upper.Up + upper.Down + lower.Up + lower.Down + gap + 2f * clearance;
            return Mathf.Max(0f, area.height - taken) * 0.5f;
        }
    }
}
