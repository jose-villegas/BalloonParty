using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>What took the cap off a meadow mushroom (<see cref="MushroomCutMessage.Cause" />).</summary>
    internal enum MushroomCutCause
    {
        /// <summary>A lance flew through the cap and cut it in two along its line.</summary>
        Lance,

        /// <summary>A blast blew the cap off whole: a bomb next to it, or a nuke anywhere on the board.</summary>
        Blast,
    }

    /// <summary>
    ///     A meadow mushroom lost its cap (@ref plan_meadow_mushrooms): a lance cut it, or a blast blew it off.
    ///     Published once for each cap, when it goes. The cap has already left the board's bumpers when this is
    ///     heard, and until <see cref="MushroomRegrownMessage" /> names the same slot the mushroom is a bare stem
    ///     and nothing bounces off it.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         <b>A lance's cut</b> (<see cref="MushroomCutCause.Lance" />). <see cref="Point" /> and
    ///         <see cref="Direction" /> give the line the cap splits along: the mushroom's view draws the two
    ///         pieces either side of it, so a lance that clips the cap's edge takes a sliver off and one through
    ///         its middle halves it.
    ///     </para>
    ///     <para>
    ///         <b>A blast</b> (<see cref="MushroomCutCause.Blast" />). Nothing split the cap, so there is no line:
    ///         <see cref="Point" /> is the cap's own centre and <see cref="Direction" /> is the way the cap is
    ///         blown, the unit direction from the blast's centre to the cap. A view shows the cap thrown off whole
    ///         that way. A bomb blows off the caps of the mushrooms next to it, the ones in the six slots round
    ///         its own, and sways the others its blast reaches (<see cref="MushroomSwayedMessage" />); a nuke
    ///         blows off each cap as its blast reaches it. Several caps blown by one bomb arrive as several
    ///         messages in the same frame, each with its own direction.
    ///     </para>
    ///     <para>
    ///         <b>A lance's cut banks a charge</b> (<see cref="Charges" />). Every cap a flight cuts saves that
    ///         shot one lance charge, whatever armed the lance: when its pierce would end at a wall, a charge
    ///         is spent and it stays a lance. The charge is already banked when this is heard.
    ///         <see cref="Charges" /> counts them for the flight, 1 for the first cap it cuts, so whoever shows
    ///         or sounds the bank can step with it. A blast banks nothing.
    ///     </para>
    ///     <para>
    ///         Whatever the cause, the cap grows back the same way: on the board's beat, the fifth after it
    ///         went, a beat later for each beat a ball is in its way.
    ///     </para>
    /// </remarks>
    internal readonly struct MushroomCutMessage
    {
        /// <summary>The cap's grid slot, as <see cref="MushroomBouncedMessage.Slot" /> names it.</summary>
        public readonly Vector2Int Slot;

        /// <summary>
        ///     For a lance: a point inside the cap that the split passes through, in world units; with
        ///     <see cref="Direction" /> it gives the line the cap splits along. It is the point of the lance's
        ///     line nearest the cap's centre (the line carried on both ways, so it can lie behind a lance that
        ///     began its step inside the circle), drawn in toward the centre when the line passes outside the cap
        ///     and only the ball's edge clipped it. It is not always a point the ball's centre flew through.
        ///     For a blast: the cap's centre, in world units.
        /// </summary>
        public readonly Vector2 Point;

        /// <summary>
        ///     A unit direction in world space. For a lance, its direction of travel. For a blast, the way the
        ///     cap is blown: from the blast's centre to the cap's (straight up for a blast centred on the cap).
        /// </summary>
        public readonly Vector2 Direction;

        /// <summary>What took the cap off: it decides what <see cref="Point" /> and <see cref="Direction" />
        /// mean.</summary>
        public readonly MushroomCutCause Cause;

        /// <summary>
        ///     How many lance charges the flight that cut this cap has banked by cutting caps, this one included:
        ///     1 for the first cap a flight cuts, 2 for its second, and so on, so above 0 says this cut banked
        ///     a charge and the number says which. It counts what the flight's cuts earned, not what the shot
        ///     still holds: a charge spent at a wall is not taken off it, and a Snipe picked up mid-pierce is
        ///     not in it. 0 when nothing was banked: a blast, or a cut no flight made (one staged by hand).
        /// </summary>
        public readonly int Charges;

        /// <summary>A lance's cut that no flight made, so one that banks nothing.</summary>
        public MushroomCutMessage(Vector2Int slot, Vector2 point, Vector2 direction)
            : this(slot, point, direction, MushroomCutCause.Lance)
        {
        }

        /// <summary>A cut that banks nothing: a blast's.</summary>
        public MushroomCutMessage(Vector2Int slot, Vector2 point, Vector2 direction, MushroomCutCause cause)
            : this(slot, point, direction, cause, 0)
        {
        }

        public MushroomCutMessage(
            Vector2Int slot, Vector2 point, Vector2 direction, MushroomCutCause cause, int charges)
        {
            Slot = slot;
            Point = point;
            Direction = direction;
            Cause = cause;
            Charges = charges;
        }
    }
}
