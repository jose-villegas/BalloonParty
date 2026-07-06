namespace BalloonParty.Shared.Messages
{
    /// <summary>Broadcast on a run restart or level transition; actors with pooled views return to pool and vacate their grid slot.</summary>
    public readonly struct BoardClearMessage
    {
        /// <summary>True during the level-transition Ascent (visible pop burst); false on a silent run-restart clear.</summary>
        public readonly bool PlayPopVfx;

        public BoardClearMessage(bool playPopVfx = false)
        {
            PlayPopVfx = playPopVfx;
        }
    }
}
