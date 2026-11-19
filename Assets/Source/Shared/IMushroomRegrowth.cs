using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The caps a lance has cut (<see cref="IBumperField.Cut" />) and that have not grown back yet, for
    ///     whoever keeps the time they grow back by (@ref plan_meadow_mushrooms). The field holds the caps and
    ///     counts what it is told; it keeps no clock of its own.
    /// </summary>
    /// <remarks>
    ///     A cap that leaves the board while cut (the level ended, the run was reset) is dropped from the list
    ///     without being regrown: the level that follows places whole caps of its own.
    /// </remarks>
    internal interface IMushroomRegrowth
    {
        /// <summary>How many caps are cut, in the order they were cut: a cap cut later has a higher index until
        /// one before it grows back. Asking costs one comparison while none is.</summary>
        int CutCount { get; }

        /// <summary>
        ///     One of the board's beats has fallen: the first <paramref name="caps" /> cut caps are a beat nearer
        ///     to growing back. The ones after them were cut too late for this beat to be theirs.
        /// </summary>
        void CountBeat(int caps);

        /// <summary>Whether the cut cap at <paramref name="index" /> has waited its beats out
        /// (<c>IMushroomSettings.RegrowBeats</c>) and may grow back.</summary>
        bool IsDueAt(int index);

        /// <summary>The circle the cut cap at <paramref name="index" /> will have again, for telling whether
        /// a ball is in its way.</summary>
        BumperCircle CutCircleAt(int index);

        /// <summary>The grid slot of the cut cap at <paramref name="index" />.</summary>
        Vector2Int CutSlotAt(int index);

        /// <summary>
        ///     The cut cap at <paramref name="index" /> is whole again: it is back in
        ///     <see cref="IBumperField.Bumpers" /> when this returns, with the id and the wall it had, and the cut
        ///     caps after it have moved down by one.
        /// </summary>
        void Regrow(int index);
    }
}
