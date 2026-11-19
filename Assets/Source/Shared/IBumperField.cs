using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The permanent bumpers on the board (the meadow's mushrooms) as a ball meets them: circles its centre
    ///     bounces off. The flight asks it on every step, so an implementation answers from a cache and
    ///     allocates nothing.
    /// </summary>
    /// <remarks>
    ///     A lance cuts caps (<see cref="Cut" />), and a cut cap is no bumper until it has grown back: it is not in
    ///     <see cref="Bumpers" />, so nothing bounces off it, counts it, is kicked toward it or looks ahead to it.
    ///     Everything that reads the field reads the same list, so a cap is gone for the flight, the aim line and
    ///     the shot solver at once.
    /// </remarks>
    internal interface IBumperField
    {
        /// <summary>
        ///     Every whole cap, in the order of their ids. A cap keeps its id and its wall while others are cut,
        ///     so an index is not an id: ask <see cref="BumperCircle.Id" />. Empty on a level without mushrooms and
        ///     while every cap is cut. The list is the field's own and is refilled when the board changes: read
        ///     it, do not keep it.
        /// </summary>
        IReadOnlyList<BumperCircle> Bumpers { get; }

        /// <summary>The grid slot of the bumper at <paramref name="index" /> of <see cref="Bumpers" />.</summary>
        Vector2Int SlotAt(int index);

        /// <summary>
        ///     A lance cut the cap at <paramref name="index" /> of <see cref="Bumpers" />: it has left the list
        ///     when this returns, and the indices after it have moved down by one. Its slot stays taken (the stem
        ///     stands there) and the caps left of its wall keep their wall.
        /// </summary>
        /// <remarks>
        ///     The list is the same one <see cref="Bumpers" /> answered before the call, one cap shorter: a
        ///     caller sweeping it for the next cap goes on with what it holds. A field that cannot cut the cap
        ///     (an index it does not have) leaves the list as it was, which a caller reads as nothing cut.
        /// </remarks>
        void Cut(int index);
    }
}
