namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     Broadcast on a run restart (or a mid-game level transition). Every actor that owns a
    ///     pooled view (balloons, static actors) returns itself to its pool and vacates its grid
    ///     slot when it receives this, so the board-clear honours the "the consumer that Get()s
    ///     it Return()s it" contract.
    /// </summary>
    public readonly struct BoardClearMessage
    {
        /// <summary>Balloons play their pop VFX before returning to pool — the level-transition
        /// Ascent wants a visible burst; a silent run-restart clear does not.</summary>
        public readonly bool PlayPopVfx;

        public BoardClearMessage(bool playPopVfx = false)
        {
            PlayPopVfx = playPopVfx;
        }
    }
}
