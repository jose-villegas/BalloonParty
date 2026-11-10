using System;
using BalloonParty.Configuration.HeadsUp;

namespace BalloonParty.Game.HeadsUp
{
    /// <summary>
    ///     Draws the heads-ups the service lets through. Implemented by <c>UI/HeadsUp/HeadsUpLayer</c>. Key lines
    ///     by the whole handle, slot and generation: a released or evicted line keeps fading after the service has
    ///     given its slot to the next line, so one slot can have two lines on screen.
    /// </summary>
    internal interface IHeadsUpPresenter
    {
        /// <summary>
        ///     Fires exactly once for every shown line that wasn't released, evicted or cleared first: when it has
        ///     faded out by itself, or at once from inside <see cref="Show" /> when it can't be placed (an endpoint
        ///     not registered yet, no camera). Its slot is free again; a line that never reports holds it forever.
        /// </summary>
        IObservable<HeadsUpHandle> Finished { get; }

        void Show(HeadsUpHandle handle, in HeadsUpRequest request, HeadsUpStyleSettings style);

        /// <summary>
        ///     The showing line of <paramref name="handle" /> writes the request's number instead. It stays where it
        ///     is, whatever the request's anchor (a streak along a pierce would otherwise move at ball speed), and
        ///     its write and hold start over, bringing back a line already fading. Several in one frame (a pierce
        ///     ploughing a row) should cost one rewrite: keep the latest and apply it in <see cref="Advance" />; an
        ///     unchanged one (a held <c>ready</c>) does nothing.
        /// </summary>
        void Rewrite(HeadsUpHandle handle, in HeadsUpRequest request, HeadsUpStyleSettings style);

        /// <summary>Fades the line out early: released, or evicted for a heads-up that matters more.</summary>
        void Release(HeadsUpHandle handle);

        void ClearAll(bool animated);

        /// <summary>
        ///     Moves every line on. Writing, holding and a line's own fade run on <paramref name="liveDelta" />, zero
        ///     under a pause, so they freeze with the game, and a line shown under a pause waits unwritten. Released,
        ///     evicted and cleared lines fade on <paramref name="unscaledDelta" />, which always runs, so a line a
        ///     <c>-1</c> pushes out during a pause doesn't stay frozen beside it.
        /// </summary>
        void Advance(float liveDelta, float unscaledDelta);
    }
}
