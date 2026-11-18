using System;

namespace BalloonParty.Balloon.View
{
    /// <summary>
    ///     A view binding with a look of its own to play between its balloon being popped and the pop being shown (a
    ///     ripe cluster folding into one big bubble). The balloon is out of play throughout: off the grid, so the
    ///     prelude must not read its slot. Binding another model or despawning drops a prelude in progress without
    ///     calling back.
    /// </summary>
    internal interface IBalloonPopPrelude
    {
        /// <summary>Starts the prelude for the balloon just popped and calls <paramref name="onDone" /> when the pop
        /// should show. False, without calling it, when this balloon has none.</summary>
        bool TryPlayPopPrelude(Action onDone);
    }
}
